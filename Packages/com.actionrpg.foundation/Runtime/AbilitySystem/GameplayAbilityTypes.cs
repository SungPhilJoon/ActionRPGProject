using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Core.AbilitySystem
{
    /// <summary>언리얼 EGameplayAbilityInstancingPolicy.</summary>
    public enum GameplayAbilityInstancingPolicy : byte
    {
        /// <summary>에셋 자체를 실행한다. 멤버 상태를 가질 수 없고 어빌리티 태스크도 쓸 수 없다.</summary>
        NonInstanced = 0,
        /// <summary>액터마다 복제본 하나를 만들어 재사용한다. 대부분의 어빌리티가 이것을 쓴다.</summary>
        InstancedPerActor = 1,
        /// <summary>발동할 때마다 새 복제본을 만든다.</summary>
        InstancedPerExecution = 2
    }

    /// <summary>언리얼 EGameplayAbilityNetExecutionPolicy.</summary>
    public enum GameplayAbilityNetExecutionPolicy : byte
    {
        /// <summary>소유 클라이언트가 먼저 실행(예측)하고 서버도 실행한다. 서버가 거절하면 클라이언트가 되돌린다.</summary>
        LocalPredicted = 0,
        /// <summary>소유 클라이언트에서만 실행한다.</summary>
        LocalOnly = 1,
        /// <summary>서버가 먼저 실행하고 소유 클라이언트에게 실행을 지시한다.</summary>
        ServerInitiated = 2,
        /// <summary>서버에서만 실행한다.</summary>
        ServerOnly = 3
    }

    /// <summary>언리얼 EGameplayAbilityActivationMode.</summary>
    public enum GameplayAbilityActivationMode : byte
    {
        /// <summary>서버(또는 리슨 서버 호스트)에서 권한을 가지고 실행 중.</summary>
        Authority = 0,
        /// <summary>권한 없이 실행 중 (ServerInitiated 를 받은 클라이언트).</summary>
        NonAuthority = 1,
        /// <summary>클라이언트가 예측 실행 중. 서버 응답 대기.</summary>
        Predicting = 2,
        /// <summary>서버가 예측을 승인함.</summary>
        Confirmed = 3,
        /// <summary>서버가 예측을 거절함.</summary>
        Rejected = 4
    }

    /// <summary>언리얼 EGameplayAbilityTriggerSource.</summary>
    public enum GameplayAbilityTriggerSource : byte
    {
        GameplayEvent = 0,
        OwnedTagAdded = 1,
        OwnedTagPresent = 2
    }

    /// <summary>언리얼 FAbilityTriggerData.</summary>
    [Serializable]
    public class AbilityTriggerData
    {
        public GameplayTag TriggerTag;
        public GameplayAbilityTriggerSource TriggerSource = GameplayAbilityTriggerSource.GameplayEvent;
    }

    /// <summary>언리얼 FGameplayAbilitySpecHandle.</summary>
    public readonly struct GameplayAbilitySpecHandle : IEquatable<GameplayAbilitySpecHandle>
    {
        public readonly int Id;

        public static readonly GameplayAbilitySpecHandle Invalid = default;

        public GameplayAbilitySpecHandle(int id) { Id = id; }

        public bool IsValid => Id != 0;
        public bool Equals(GameplayAbilitySpecHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is GameplayAbilitySpecHandle other && Equals(other);
        public override int GetHashCode() => Id;
        public override string ToString() => IsValid ? $"GA#{Id}" : "GA#invalid";

        public static bool operator ==(GameplayAbilitySpecHandle a, GameplayAbilitySpecHandle b) => a.Id == b.Id;
        public static bool operator !=(GameplayAbilitySpecHandle a, GameplayAbilitySpecHandle b) => a.Id != b.Id;
    }

    /// <summary>언리얼 FGameplayAbilityActivationInfo.</summary>
    public struct GameplayAbilityActivationInfo
    {
        public GameplayAbilityActivationMode ActivationMode;
        public PredictionKey ActivationPredictionKey;

        public GameplayAbilityActivationInfo(GameplayAbilityActivationMode mode, PredictionKey key)
        {
            ActivationMode = mode;
            ActivationPredictionKey = key;
        }

        public override string ToString() => $"{ActivationMode} {ActivationPredictionKey}";
    }

    /// <summary>
    /// 언리얼 FGameplayAbilitySpec. ASC 가 "가지고 있는" 어빌리티 하나.
    /// 서버가 GiveAbility 로 만들고 소유 클라이언트에 복제된다.
    /// </summary>
    public sealed class GameplayAbilitySpec
    {
        public GameplayAbilitySpecHandle Handle { get; internal set; }
        /// <summary>어빌리티 에셋 (언리얼의 CDO).</summary>
        public GameplayAbility Ability { get; }
        public int Level { get; set; }
        /// <summary>입력 슬롯. -1 이면 입력과 연결되지 않는다.</summary>
        public int InputID { get; set; }
        public UnityEngine.Object SourceObject { get; set; }
        public readonly GameplayTagContainer DynamicAbilityTags = new GameplayTagContainer();

        /// <summary>현재 몇 번 실행 중인지 (InstancedPerExecution 은 여러 개일 수 있다).</summary>
        public int ActiveCount { get; internal set; }
        public bool IsActive => ActiveCount > 0;
        public bool InputPressed { get; internal set; }
        public bool PendingRemove { get; internal set; }

        /// <summary>가장 최근 발동 정보.</summary>
        public GameplayAbilityActivationInfo ActivationInfo { get; internal set; }

        internal readonly List<GameplayAbility> Instances = new List<GameplayAbility>(1);

        public GameplayAbilitySpec(GameplayAbility ability, int level = 1, int inputId = -1, UnityEngine.Object sourceObject = null)
        {
            Ability = ability;
            Level = level;
            InputID = inputId;
            SourceObject = sourceObject;
        }

        /// <summary>InstancedPerActor 의 복제본. 다른 정책이면 첫 인스턴스 또는 null.</summary>
        public GameplayAbility GetPrimaryInstance() => Instances.Count > 0 ? Instances[0] : null;

        public IReadOnlyList<GameplayAbility> GetAbilityInstances() => Instances;

        public override string ToString() => $"{Handle} {(Ability != null ? Ability.name : "null")} Lv{Level} Input{InputID}{(IsActive ? " (active)" : string.Empty)}";
    }

    /// <summary>
    /// 언리얼 FGameplayAbilityActorInfo. 어빌리티가 자주 쓰는 참조를 모아 둔 것.
    /// 이 프로젝트에서는 Owner 와 Avatar 가 같은 캐릭터 GameObject 다.
    /// </summary>
    public sealed class GameplayAbilityActorInfo
    {
        public GameObject OwnerActor { get; internal set; }
        public GameObject AvatarActor { get; internal set; }
        public AbilitySystemComponent AbilitySystemComponent { get; internal set; }
        public IAbilityAnimationDriver AnimationDriver { get; internal set; }

        /// <summary>이 피어가 이 캐릭터를 조종하는가 (플레이어의 소유 클라이언트, 또는 서버의 AI).</summary>
        public bool IsLocallyControlled => AbilitySystemComponent != null && AbilitySystemComponent.IsLocallyControlled;

        /// <summary>이 피어가 권한(서버)을 가졌는가. 리슨 서버 호스트는 항상 true.</summary>
        public bool IsNetAuthority => AbilitySystemComponent != null && AbilitySystemComponent.IsAuthority;

        public T GetAvatarComponent<T>() where T : class => AvatarActor != null ? AvatarActor.GetComponent<T>() : null;
    }

    /// <summary>
    /// 언리얼 FGameplayAbilityTargetData 의 단순화 버전. 클라이언트가 고른 조준 정보를 서버로 보낸다.
    /// </summary>
    [Serializable]
    public struct GameplayAbilityTargetData : INetworkSerializable
    {
        public Vector3 Origin;
        public Vector3 Direction;
        public Vector3 TargetPoint;
        public ulong TargetObjectId;
        public bool HasTargetPoint;
        public bool HasTargetObject;

        public static GameplayAbilityTargetData FromDirection(Vector3 origin, Vector3 direction) =>
            new GameplayAbilityTargetData { Origin = origin, Direction = direction };

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Origin);
            serializer.SerializeValue(ref Direction);
            serializer.SerializeValue(ref TargetPoint);
            serializer.SerializeValue(ref TargetObjectId);
            serializer.SerializeValue(ref HasTargetPoint);
            serializer.SerializeValue(ref HasTargetObject);
        }

        /// <summary>대상 오브젝트를 찾는다. 없으면 null.</summary>
        public GameObject GetTargetObject()
        {
            if (!HasTargetObject || NetworkManager.Singleton == null) return null;
            return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(TargetObjectId, out var netObject) && netObject != null
                ? netObject.gameObject
                : null;
        }
    }

    /// <summary>
    /// 언리얼 FGameplayEventData. 게임플레이 이벤트의 내용물. 어빌리티를 이벤트로 발동시킬 때와
    /// 입력으로 발동할 때 조준 정보를 실어 보낼 때 쓴다.
    /// </summary>
    [Serializable]
    public struct GameplayEventData : INetworkSerializable
    {
        public int EventTagHash;
        public ulong InstigatorId;
        public ulong TargetId;
        public float EventMagnitude;
        public bool HasTargetData;
        public GameplayAbilityTargetData TargetData;

        public GameplayTag EventTag
        {
            get => GameplayTag.FromHash(EventTagHash);
            set => EventTagHash = value.Hash;
        }

        public static GameplayEventData WithTargetData(in GameplayAbilityTargetData targetData) =>
            new GameplayEventData { HasTargetData = true, TargetData = targetData };

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref EventTagHash);
            serializer.SerializeValue(ref InstigatorId);
            serializer.SerializeValue(ref TargetId);
            serializer.SerializeValue(ref EventMagnitude);
            serializer.SerializeValue(ref HasTargetData);
            if (HasTargetData) serializer.SerializeValue(ref TargetData);
        }
    }

    /// <summary>
    /// 어빌리티 태스크(PlayMontageAndWait)가 애니메이션을 재생하는 통로. 캐릭터 쪽 애니메이터 드라이버가 구현한다.
    /// 언리얼의 AnimInstance / Montage 에 해당한다.
    /// </summary>
    public interface IAbilityAnimationDriver
    {
        /// <summary>triggerHash 트리거를 당기고, indexParamHash 가 0 이 아니면 그 int 파라미터에 index 를 넣는다.</summary>
        void PlayAbilityAnimation(int triggerHash, int indexParamHash, int index);
    }

    /// <summary>언리얼 IAbilitySystemInterface. ASC 를 직접 붙이지 않은 오브젝트가 ASC 를 알려 줄 때 구현한다.</summary>
    public interface IAbilitySystemInterface
    {
        AbilitySystemComponent GetAbilitySystemComponent();
    }
}
