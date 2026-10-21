using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace ActionRPG.Core.AbilitySystem
{
    /// <summary>
    /// 언리얼 FPredictionKey. 클라이언트가 결과를 미리 보여 준 행동(어빌리티 발동, 쿨다운, 비용)을
    /// 서버가 승인/거절했는지 짝 맞추는 번호다. 0 은 "예측 아님".
    /// </summary>
    [Serializable]
    public struct PredictionKey : IEquatable<PredictionKey>, INetworkSerializable
    {
        public int Current;

        public static readonly PredictionKey None = default;

        public PredictionKey(int value) { Current = value; }

        public bool IsValidKey => Current > 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter => serializer.SerializeValue(ref Current);

        public bool Equals(PredictionKey other) => Current == other.Current;
        public override bool Equals(object obj) => obj is PredictionKey other && Equals(other);
        public override int GetHashCode() => Current;
        public override string ToString() => IsValidKey ? $"PK{Current}" : "PK(none)";
    }

    /// <summary>언리얼 FActiveGameplayEffectHandle.</summary>
    public readonly struct ActiveGameplayEffectHandle : IEquatable<ActiveGameplayEffectHandle>
    {
        public readonly int Id;

        public static readonly ActiveGameplayEffectHandle Invalid = default;

        public ActiveGameplayEffectHandle(int id) { Id = id; }

        public bool IsValid => Id != 0;
        public bool Equals(ActiveGameplayEffectHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is ActiveGameplayEffectHandle other && Equals(other);
        public override int GetHashCode() => Id;
        public override string ToString() => IsValid ? $"GE#{Id}" : "GE#invalid";

        public static bool operator ==(ActiveGameplayEffectHandle a, ActiveGameplayEffectHandle b) => a.Id == b.Id;
        public static bool operator !=(ActiveGameplayEffectHandle a, ActiveGameplayEffectHandle b) => a.Id != b.Id;
    }

    /// <summary>
    /// 언리얼 FActiveGameplayEffect. 대상에게 "지금 걸려 있는" 지속형 이펙트 하나.
    /// 서버에서는 실제 상태이고, 클라이언트에서는 두 종류가 섞인다.
    ///  - 예측 이펙트(IsPredicted)   : 클라이언트가 먼저 걸어 둔 것. 서버 결과가 오면 지운다.
    ///  - 복제 사본(IsReplicatedProxy): 서버 목록을 받아 만든 것. UI 조회용이며 어트리뷰트·태그에 관여하지 않는다.
    /// </summary>
    public sealed class ActiveGameplayEffect
    {
        public ActiveGameplayEffectHandle Handle { get; }
        public GameplayEffectSpec Spec { get; }
        public PredictionKey PredictionKey { get; }
        public bool IsReplicatedProxy { get; }
        public bool IsPredicted => !IsReplicatedProxy && PredictionKey.IsValidKey && !IsAuthorityOwned;

        /// <summary>서버에서 만들어진 실제 이펙트인지.</summary>
        public bool IsAuthorityOwned { get; }

        public double StartWorldTime { get; internal set; }
        public bool IsInhibited { get; internal set; }
        public bool IsPendingRemove { get; internal set; }

        internal double NextPeriodTime;
        internal readonly GameplayTagContainer GrantedTags = new GameplayTagContainer();
        internal readonly GameplayTagContainer AllOwnedTags = new GameplayTagContainer();
        internal readonly List<GameplayAbilitySpecHandle> GrantedAbilityHandles = new List<GameplayAbilitySpecHandle>();
        internal readonly List<int> ActiveCueInstanceIds = new List<int>();

        /// <summary>복제 사본일 때 서버가 알려 준 시전자 NetworkObjectId.</summary>
        public ulong ReplicatedInstigatorId { get; internal set; }

        internal ActiveGameplayEffect(ActiveGameplayEffectHandle handle, GameplayEffectSpec spec, double startTime,
            PredictionKey predictionKey, bool authorityOwned, bool replicatedProxy)
        {
            Handle = handle;
            Spec = spec;
            StartWorldTime = startTime;
            PredictionKey = predictionKey;
            IsAuthorityOwned = authorityOwned;
            IsReplicatedProxy = replicatedProxy;
            RefreshCachedTags();
        }

        internal void RefreshCachedTags()
        {
            GrantedTags.Reset();
            AllOwnedTags.Reset();
            Spec.GetAllGrantedTags(GrantedTags);
            Spec.GetAllAssetTags(AllOwnedTags);
            AllOwnedTags.AppendTags(GrantedTags);
        }

        public GameplayEffect Def => Spec.Def;
        public int StackCount => Spec.StackCount;
        public float Duration => Spec.Duration;
        public bool IsInfinite => Spec.Duration < 0f;

        public double EndTime => Spec.Duration > 0f ? StartWorldTime + Spec.Duration : double.MaxValue;

        public float GetTimeRemaining(double worldTime)
        {
            if (Spec.Duration < 0f) return -1f;
            return (float)Math.Max(0d, EndTime - worldTime);
        }

        /// <summary>이펙트의 모든 태그(AssetTags + GrantedTags + 동적 태그).</summary>
        public GameplayTagContainer GetAllOwnedTags() => AllOwnedTags;

        public override string ToString() =>
            $"{Handle} {(Def != null ? Def.name : "?")} x{StackCount}{(IsInhibited ? " (inhibited)" : string.Empty)}{(IsPredicted ? " (predicted)" : string.Empty)}{(IsReplicatedProxy ? " (rep)" : string.Empty)}";
    }

    /// <summary>
    /// 언리얼 FGameplayEffectQuery 의 단순화 버전. 활성 이펙트를 고를 때 쓴다.
    /// 쿨다운 남은 시간 조회(<c>MakeQuery_MatchAnyOwningTags(cooldownTags)</c>)가 대표적인 쓰임이다.
    /// </summary>
    public sealed class GameplayEffectQuery
    {
        /// <summary>이 중 하나라도 이펙트 태그와 맞으면 통과. 비어 있으면 무시.</summary>
        public GameplayTagContainer MatchAnyOwningTags;
        /// <summary>이 태그를 모두 가져야 통과. 비어 있으면 무시.</summary>
        public GameplayTagContainer MatchAllOwningTags;
        /// <summary>이 중 하나라도 가지면 탈락.</summary>
        public GameplayTagContainer MatchNoOwningTags;
        public GameplayEffect EffectDefinition;
        public AbilitySystemComponent EffectSource;
        public Func<ActiveGameplayEffect, bool> CustomMatch;

        public static GameplayEffectQuery MakeQuery_MatchAnyOwningTags(GameplayTagContainer tags) =>
            new GameplayEffectQuery { MatchAnyOwningTags = tags };

        public static GameplayEffectQuery MakeQuery_MatchAllOwningTags(GameplayTagContainer tags) =>
            new GameplayEffectQuery { MatchAllOwningTags = tags };

        public static GameplayEffectQuery MakeQuery_MatchNoOwningTags(GameplayTagContainer tags) =>
            new GameplayEffectQuery { MatchNoOwningTags = tags };

        public static GameplayEffectQuery MakeQuery_EffectDefinition(GameplayEffect definition) =>
            new GameplayEffectQuery { EffectDefinition = definition };

        public static GameplayEffectQuery MakeQuery_Custom(Func<ActiveGameplayEffect, bool> match) =>
            new GameplayEffectQuery { CustomMatch = match };

        public bool Matches(ActiveGameplayEffect effect)
        {
            if (effect == null || effect.IsPendingRemove) return false;
            var tags = effect.GetAllOwnedTags();
            if (MatchAnyOwningTags != null && !MatchAnyOwningTags.IsEmpty && !tags.HasAny(MatchAnyOwningTags)) return false;
            if (MatchAllOwningTags != null && !MatchAllOwningTags.IsEmpty && !tags.HasAll(MatchAllOwningTags)) return false;
            if (MatchNoOwningTags != null && !MatchNoOwningTags.IsEmpty && tags.HasAny(MatchNoOwningTags)) return false;
            if (EffectDefinition != null && effect.Def != EffectDefinition) return false;
            if (EffectSource != null && effect.Spec.Context.InstigatorAbilitySystemComponent != EffectSource) return false;
            if (CustomMatch != null && !CustomMatch(effect)) return false;
            return true;
        }
    }
}
