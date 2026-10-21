using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Core.AbilitySystem
{
    /// <summary>언리얼 EGameplayEffectReplicationMode.</summary>
    public enum GameplayEffectReplicationMode : byte
    {
        /// <summary>활성 이펙트 목록을 아무에게도 복제하지 않는다. 태그 · 큐 · 어트리뷰트만 복제. (AI 권장)</summary>
        Minimal = 0,
        /// <summary>활성 이펙트 목록은 소유 클라이언트에게만, 태그 · 큐 · 어트리뷰트는 모두에게. (플레이어 권장)</summary>
        Mixed = 1,
        /// <summary>활성 이펙트 목록까지 모두에게 복제.</summary>
        Full = 2
    }

    /// <summary>
    /// 언리얼 UAbilitySystemComponent 를 Netcode for GameObjects 위에 구현한 것.
    ///
    /// 네트워크 모델은 언리얼의 리슨 서버와 같다.
    ///  - 서버(= 리슨 서버의 호스트)가 어트리뷰트 · 이펙트 · 태그 · 어빌리티 부여의 유일한 권한자다.
    ///  - 호스트 자신의 캐릭터는 권한을 가진 채로 바로 실행되므로 예측이 필요 없다.
    ///  - 원격 클라이언트는 LocalPredicted 어빌리티를 먼저 실행(예측)하고 서버에 ServerTryActivateAbility 를 보낸다.
    ///    서버가 승인/거절을 돌려주고, 예측으로 미리 걸어 둔 쿨다운 · 비용은 서버 상태가 도착하면 정리된다.
    ///  - AI 는 서버가 소유하므로 서버에서 권한을 가지고 실행된다.
    ///
    /// 파일 구성:
    ///   AbilitySystemComponent.cs              — 수명주기, 어트리뷰트, 태그
    ///   AbilitySystemComponent.Effects.cs      — 게임플레이 이펙트 적용 · 집계 · 만료
    ///   AbilitySystemComponent.Abilities.cs    — 어빌리티 부여 · 발동 · 예측 · RPC
    ///   AbilitySystemComponent.Replication.cs  — 복제 목록, 게임플레이 큐, 애니메이션 전파
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public partial class AbilitySystemComponent : NetworkBehaviour
    {
        [Tooltip("스폰 시 부여할 구성. 어트리뷰트 세트는 모든 피어에서, 어빌리티 · 이펙트는 서버에서 처리된다.")]
        [SerializeField] private AbilitySet[] startupAbilitySets = Array.Empty<AbilitySet>();
        [SerializeField] private GameplayEffectReplicationMode replicationMode = GameplayEffectReplicationMode.Mixed;

        // ---------------------------------------------------------------- 어트리뷰트

        private sealed class AttributeEntry
        {
            public int Index;
            public AttributeSet Set;
            public GameplayAttribute Attribute;
            public GameplayAttributeData Data;
            public bool IsMeta;
            public GameplayAttributeValueChangeDelegate Delegate;

            // 서버: 마지막으로 보낸 값
            public float SentBase = float.NaN;
            public float SentCurrent = float.NaN;
            public int RepListIndex = -1;

            // 클라이언트: 서버가 보낸 값
            public float RepBase;
            public float RepCurrent;
            public bool HasRepValue;
        }

        private readonly List<AttributeSet> _spawnedAttributes = new List<AttributeSet>(4);
        private readonly List<AttributeEntry> _attributes = new List<AttributeEntry>(32);
        private readonly Dictionary<string, AttributeEntry> _attributesByName = new Dictionary<string, AttributeEntry>(32);

        // ---------------------------------------------------------------- 태그

        private readonly GameplayTagCountContainer _ownedTags = new GameplayTagCountContainer();
        private readonly GameplayTagCountContainer _blockedAbilityTags = new GameplayTagCountContainer();
        private readonly GameplayTagContainer _scratchTags = new GameplayTagContainer();
        private bool _evaluatingTagDependencies;

        // ---------------------------------------------------------------- 공통

        private readonly AbilitySet.GrantedHandles _startupHandles = new AbilitySet.GrantedHandles();
        private bool _initialized;

        public GameplayAbilityActorInfo AbilityActorInfo { get; } = new GameplayAbilityActorInfo();
        public GameplayEffectReplicationMode ReplicationMode => replicationMode;
        public IReadOnlyList<AttributeSet> SpawnedAttributes => _spawnedAttributes;

        /// <summary>
        /// 이 피어가 권한을 가졌는가. 네트워크가 꺼져 있으면(오프라인 테스트) true.
        /// 리슨 서버 호스트는 모든 ASC 에 대해 true 다.
        /// </summary>
        public bool IsAuthority
        {
            get
            {
                var nm = NetworkManager.Singleton;
                if (nm == null || !nm.IsListening) return true;
                return nm.IsServer;
            }
        }

        /// <summary>이 피어가 조종하는가. 플레이어는 소유 클라이언트, AI 는 서버.</summary>
        public bool IsLocallyControlled
        {
            get
            {
                var nm = NetworkManager.Singleton;
                if (nm == null || !nm.IsListening || !IsSpawned) return true;
                return IsOwner;
            }
        }

        /// <summary>이펙트 시간 계산에 쓰는 시계. 네트워크 중이면 서버 시간, 아니면 로컬 시간.</summary>
        public double WorldTime
        {
            get
            {
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsListening) return nm.ServerTime.Time;
                return Time.timeAsDouble;
            }
        }

        // ================================================================ 수명주기

        protected virtual void Awake()
        {
            AbilitySystemGlobals.Get();
            CreateReplicationLists();
            InitAbilityActorInfo(gameObject, gameObject);

            for (int i = 0; i < startupAbilitySets.Length; i++)
                if (startupAbilitySets[i] != null) startupAbilitySets[i].CreateAttributeSets(this);

            _ownedTags.OnAnyTagNewOrRemove += OnOwnedTagNewOrRemoved;
            _initialized = true;
        }

        /// <summary>언리얼 InitAbilityActorInfo. 이 프로젝트에서는 소유자와 아바타가 같은 캐릭터다.</summary>
        public void InitAbilityActorInfo(GameObject owner, GameObject avatar)
        {
            AbilityActorInfo.OwnerActor = owner;
            AbilityActorInfo.AvatarActor = avatar;
            AbilityActorInfo.AbilitySystemComponent = this;
            AbilityActorInfo.AnimationDriver = avatar != null ? avatar.GetComponentInChildren<IAbilityAnimationDriver>() : null;
        }

        public override void OnNetworkSpawn()
        {
            if (IsAuthority)
            {
                InitializeServerReplication();
                for (int i = 0; i < startupAbilitySets.Length; i++)
                    if (startupAbilitySets[i] != null) startupAbilitySets[i].GiveToAbilitySystem(this, _startupHandles);
                FlushServerReplication();
            }
            else
            {
                InitializeClientReplication();
            }
        }

        public override void OnNetworkDespawn()
        {
            CancelAllAbilities(null, replicate: false);
            ShutdownReplication();
            RemoveAllLocalCues();
        }

        protected virtual void Update()
        {
            if (!_initialized) return;
            float dt = Time.deltaTime;

            if (!IsAuthority) ProcessClientReplication();

            TickActiveAbilities(dt);
            TickActiveEffects();
        }

        protected virtual void LateUpdate()
        {
            if (!_initialized) return;
            if (IsAuthority && IsSpawned) FlushServerReplication();
            else if (!IsAuthority) ProcessPredictionCatchUp();
        }

        // ================================================================ 어트리뷰트 세트

        public T GetSet<T>() where T : AttributeSet
        {
            for (int i = 0; i < _spawnedAttributes.Count; i++)
                if (_spawnedAttributes[i] is T typed) return typed;
            return null;
        }

        public T AddSet<T>() where T : AttributeSet, new() => (T)GetOrCreateAttributeSet(typeof(T));

        /// <summary>
        /// 어트리뷰트 세트를 만든다. 복제 인덱스가 생성 순서로 정해지므로 모든 피어에서 같은 순서로,
        /// 스폰 전에(Awake) 만들어야 한다.
        /// </summary>
        public AttributeSet GetOrCreateAttributeSet(Type setType)
        {
            for (int i = 0; i < _spawnedAttributes.Count; i++)
                if (_spawnedAttributes[i].GetType() == setType) return _spawnedAttributes[i];

            if (IsSpawned)
                Debug.LogWarning($"[GAS] {name}: 스폰 이후에 {setType.Name} 을 추가했습니다. 피어 간 복제 인덱스가 어긋날 수 있습니다.", this);

            var set = (AttributeSet)Activator.CreateInstance(setType);
            set.OwningAbilitySystemComponent = this;
            _spawnedAttributes.Add(set);

            var cache = AttributeSet.GetCache(setType);
            for (int i = 0; i < cache.Fields.Length; i++)
            {
                var field = cache.Fields[i];
                var data = (GameplayAttributeData)field.GetValue(set);
                if (data == null) continue;
                data.SetCurrentValueInternal(data.BaseValue);
                var entry = new AttributeEntry
                {
                    Index = _attributes.Count,
                    Set = set,
                    Attribute = new GameplayAttribute(setType.Name, field.Name),
                    Data = data,
                    IsMeta = cache.IsMeta[i],
                    Delegate = new GameplayAttributeValueChangeDelegate()
                };
                _attributes.Add(entry);
                _attributesByName[entry.Attribute.Name] = entry;
            }
            set.OnRegistered();
            return set;
        }

        public bool HasAttributeSetForAttribute(GameplayAttribute attribute) =>
            attribute.IsValid && _attributesByName.ContainsKey(attribute.Name);

        /// <summary>언리얼 GetNumericAttribute. CurrentValue.</summary>
        public float GetNumericAttribute(GameplayAttribute attribute) =>
            _attributesByName.TryGetValue(attribute.Name, out var entry) ? entry.Data.CurrentValue : 0f;

        /// <summary>언리얼 GetNumericAttributeBase. BaseValue.</summary>
        public float GetNumericAttributeBase(GameplayAttribute attribute) =>
            _attributesByName.TryGetValue(attribute.Name, out var entry) ? entry.Data.BaseValue : 0f;

        /// <summary>언리얼 SetNumericAttributeBase. 서버 전용. 이펙트를 거치지 않고 BaseValue 를 바꾼다.</summary>
        public void SetNumericAttributeBase(GameplayAttribute attribute, float newBaseValue)
        {
            if (!IsAuthority) return;
            if (_attributesByName.TryGetValue(attribute.Name, out var entry)) InternalSetBase(entry, newBaseValue, null);
        }

        /// <summary>언리얼 GetGameplayAttributeValueChangeDelegate. CurrentValue 가 바뀔 때마다 모든 피어에서 호출된다.</summary>
        public GameplayAttributeValueChangeDelegate GetGameplayAttributeValueChangeDelegate(GameplayAttribute attribute)
        {
            if (_attributesByName.TryGetValue(attribute.Name, out var entry)) return entry.Delegate;
            Debug.LogWarning($"[GAS] {name}: 어트리뷰트 {attribute} 가 없습니다.", this);
            return new GameplayAttributeValueChangeDelegate();
        }

        private AttributeEntry FindEntry(GameplayAttribute attribute) =>
            attribute.IsValid && _attributesByName.TryGetValue(attribute.Name, out var entry) ? entry : null;

        private void InternalSetBase(AttributeEntry entry, float newBase, GameplayEffectModCallbackData modData)
        {
            entry.Set.PreAttributeBaseChange(entry.Attribute, ref newBase);
            float oldBase = entry.Data.BaseValue;
            entry.Data.SetBaseValueInternal(newBase);
            if (!Mathf.Approximately(oldBase, newBase)) entry.Set.PostAttributeBaseChange(entry.Attribute, oldBase, newBase);
            UpdateAttributeCurrent(entry, modData);
        }

        /// <summary>BaseValue 와 활성 모디파이어로 CurrentValue 를 다시 계산하고, 바뀌었으면 알린다.</summary>
        private void UpdateAttributeCurrent(AttributeEntry entry, GameplayEffectModCallbackData modData = null)
        {
            float value = IsAuthority ? AggregateAuthority(entry) : AggregateClient(entry);
            entry.Set.PreAttributeChange(entry.Attribute, ref value);
            float old = entry.Data.CurrentValue;
            entry.Data.SetCurrentValueInternal(value);
            if (Mathf.Approximately(old, value)) return;
            entry.Set.PostAttributeChange(entry.Attribute, old, value);
            entry.Delegate.Broadcast(new OnAttributeChangeData(entry.Attribute, value, old, modData));
        }

        private void UpdateAttributesModifiedBy(ActiveGameplayEffect effect)
        {
            var modifiers = effect.Def.Modifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                var entry = FindEntry(modifiers[i].Attribute);
                if (entry != null) UpdateAttributeCurrent(entry);
            }
        }

        // ================================================================ 태그

        public bool HasMatchingGameplayTag(GameplayTag tag) => _ownedTags.HasMatchingGameplayTag(tag);
        public bool HasAllMatchingGameplayTags(GameplayTagContainer tags) => _ownedTags.HasAllMatchingGameplayTags(tags);
        public bool HasAnyMatchingGameplayTags(GameplayTagContainer tags) => _ownedTags.HasAnyMatchingGameplayTags(tags);
        public int GetTagCount(GameplayTag tag) => _ownedTags.GetTagCount(tag);

        public void GetOwnedGameplayTags(GameplayTagContainer output) => _ownedTags.GetExplicitTags(output);

        /// <summary>언리얼 RegisterGameplayTagEvent.</summary>
        public GameplayTagCountContainer.TagEvent RegisterGameplayTagEvent(GameplayTag tag, GameplayTagEventType eventType = GameplayTagEventType.NewOrRemoved) =>
            _ownedTags.RegisterGameplayTagEvent(tag, eventType);

        /// <summary>어떤 태그든 생기거나 없어질 때.</summary>
        public event Action<GameplayTag, int> OnAnyGameplayTagNewOrRemoved
        {
            add => _ownedTags.OnAnyTagNewOrRemove += value;
            remove => _ownedTags.OnAnyTagNewOrRemove -= value;
        }

        /// <summary>언리얼 AddLooseGameplayTag. 이 피어에만 적용된다 (복제되지 않음).</summary>
        public void AddLooseGameplayTag(GameplayTag tag, int count = 1) => _ownedTags.UpdateTagCount(tag, count);
        public void RemoveLooseGameplayTag(GameplayTag tag, int count = 1) => _ownedTags.UpdateTagCount(tag, -count);
        public void AddLooseGameplayTags(GameplayTagContainer tags, int count = 1) => _ownedTags.UpdateTagCount(tags, count);
        public void RemoveLooseGameplayTags(GameplayTagContainer tags, int count = 1) => _ownedTags.UpdateTagCount(tags, -count);

        /// <summary>언리얼 AddReplicatedLooseGameplayTag. 서버 전용. 모든 피어에 복제된다 (사망 상태 등).</summary>
        public void AddReplicatedLooseGameplayTag(GameplayTag tag)
        {
            if (!IsAuthority) return;
            UpdateReplicatedTag(tag, +1);
        }

        public void RemoveReplicatedLooseGameplayTag(GameplayTag tag)
        {
            if (!IsAuthority) return;
            UpdateReplicatedTag(tag, -1);
        }

        /// <summary>서버: 소유 태그와 복제 태그를 함께 바꾼다.</summary>
        private void UpdateReplicatedTag(GameplayTag tag, int delta)
        {
            if (!tag.IsValid || delta == 0) return;
            int hash = tag.Hash;
            _serverReplicatedTagCounts.TryGetValue(hash, out int count);
            int next = Mathf.Max(0, count + delta);
            int applied = next - count;
            if (applied == 0) return;
            if (next == 0) _serverReplicatedTagCounts.Remove(hash);
            else _serverReplicatedTagCounts[hash] = next;
            _tagsDirty = true;
            _ownedTags.UpdateTagCount(tag, applied);
        }

        private void UpdateReplicatedTags(GameplayTagContainer tags, int delta)
        {
            for (int i = 0; i < tags.Count; i++) UpdateReplicatedTag(tags[i], delta);
        }

        // ---------------------------------------------------------------- 어빌리티 블록 태그

        /// <summary>어빌리티 태그 중 하나라도 막혀 있으면 true.</summary>
        public bool AreAbilityTagsBlocked(GameplayTagContainer abilityTags)
        {
            if (abilityTags == null || abilityTags.IsEmpty) return false;
            foreach (var pair in _blockedAbilityTags.ExplicitTagCounts)
            {
                if (pair.Value <= 0) continue;
                if (abilityTags.HasTag(GameplayTag.FromHash(pair.Key))) return true;
            }
            return false;
        }

        public void BlockAbilitiesWithTags(GameplayTagContainer tags) => _blockedAbilityTags.UpdateTagCount(tags, 1);
        public void UnBlockAbilitiesWithTags(GameplayTagContainer tags) => _blockedAbilityTags.UpdateTagCount(tags, -1);

        /// <summary>언리얼 ApplyAbilityBlockAndCancelTags.</summary>
        public void ApplyAbilityBlockAndCancelTags(GameplayTagContainer abilityTags, GameplayAbility requestingAbility,
            bool enableBlockTags, GameplayTagContainer blockTags, bool executeCancelTags, GameplayTagContainer cancelTags)
        {
            if (blockTags != null && !blockTags.IsEmpty)
            {
                if (enableBlockTags) BlockAbilitiesWithTags(blockTags);
                else UnBlockAbilitiesWithTags(blockTags);
            }
            if (executeCancelTags && cancelTags != null && !cancelTags.IsEmpty)
                CancelAbilities(cancelTags, null, requestingAbility);
        }

        // ---------------------------------------------------------------- 태그 변화 → 이펙트 / 어빌리티

        private void OnOwnedTagNewOrRemoved(GameplayTag tag, int count)
        {
            if (!_evaluatingTagDependencies)
            {
                _evaluatingTagDependencies = true;
                try
                {
                    // 태그가 바뀌면 Ongoing/Removal 조건이 다시 맞는지 본다. 그 과정에서 태그가 또 바뀔 수 있어 몇 번 반복한다.
                    for (int pass = 0; pass < 4; pass++)
                        if (!ReevaluateEffectTagRequirements()) break;
                }
                finally
                {
                    _evaluatingTagDependencies = false;
                }
            }
            HandleTagTriggeredAbilities(tag, count);
        }

        // ================================================================ 디버그

        /// <summary>언리얼 "showdebug abilitysystem" 에 해당하는 요약 문자열.</summary>
        public string GetDebugString()
        {
            var sb = new StringBuilder(512);
            sb.AppendLine($"[{name}] Authority={IsAuthority} Local={IsLocallyControlled} Mode={replicationMode}");
            sb.AppendLine("— Attributes");
            for (int i = 0; i < _attributes.Count; i++)
            {
                var a = _attributes[i];
                sb.AppendLine($"  {a.Attribute.Name} = {a.Data.CurrentValue:0.##} (base {a.Data.BaseValue:0.##}){(a.IsMeta ? " [meta]" : string.Empty)}");
            }
            sb.AppendLine("— Owned Tags");
            foreach (var pair in _ownedTags.ExplicitTagCounts)
                sb.AppendLine($"  {GameplayTag.FromHash(pair.Key)} x{pair.Value}");
            sb.AppendLine("— Active Effects");
            double now = WorldTime;
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                var e = _activeEffects[i];
                string remaining = e.Duration > 0f ? $"{e.GetTimeRemaining(now):0.0}s" : e.IsInfinite ? "inf" : "-";
                sb.AppendLine($"  {e} {remaining}");
            }
            sb.AppendLine("— Abilities");
            for (int i = 0; i < _activatableAbilities.Count; i++)
                sb.AppendLine($"  {_activatableAbilities[i]}");
            return sb.ToString();
        }
    }
}
