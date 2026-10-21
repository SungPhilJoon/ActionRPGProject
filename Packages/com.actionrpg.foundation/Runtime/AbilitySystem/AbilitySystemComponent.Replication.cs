using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Core.AbilitySystem
{
    // ==================================================================== 복제 데이터

    /// <summary>어트리뷰트 하나의 복제 값. 언리얼의 어트리뷰트 프로퍼티 복제(OnRep)에 해당한다.</summary>
    public struct AttributeRepData : INetworkSerializable, IEquatable<AttributeRepData>
    {
        public ushort Index;
        public float Base;
        public float Current;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Index);
            serializer.SerializeValue(ref Base);
            serializer.SerializeValue(ref Current);
        }

        public bool Equals(AttributeRepData other) => Index == other.Index && Base.Equals(other.Base) && Current.Equals(other.Current);
    }

    /// <summary>복제되는 태그 개수 (이펙트가 준 태그 + 복제 루즈 태그). 언리얼 MinimalReplicationTags.</summary>
    public struct TagCountRepData : INetworkSerializable, IEquatable<TagCountRepData>
    {
        public int TagHash;
        public short Count;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref TagHash);
            serializer.SerializeValue(ref Count);
        }

        public bool Equals(TagCountRepData other) => TagHash == other.TagHash && Count == other.Count;
    }

    /// <summary>활성 이펙트 하나의 복제 요약. 언리얼 FActiveGameplayEffectsContainer 복제에 해당한다.</summary>
    public struct ActiveEffectRepData : INetworkSerializable, IEquatable<ActiveEffectRepData>
    {
        public int Handle;
        public int EffectId;
        public float Level;
        public float Duration;
        public double StartTime;
        public byte StackCount;
        public bool Inhibited;
        public int DynamicTag0;
        public int DynamicTag1;
        public ulong InstigatorId;
        public int PredictionKey;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Handle);
            serializer.SerializeValue(ref EffectId);
            serializer.SerializeValue(ref Level);
            serializer.SerializeValue(ref Duration);
            serializer.SerializeValue(ref StartTime);
            serializer.SerializeValue(ref StackCount);
            serializer.SerializeValue(ref Inhibited);
            serializer.SerializeValue(ref DynamicTag0);
            serializer.SerializeValue(ref DynamicTag1);
            serializer.SerializeValue(ref InstigatorId);
            serializer.SerializeValue(ref PredictionKey);
        }

        public bool Equals(ActiveEffectRepData other) =>
            Handle == other.Handle && EffectId == other.EffectId && Level.Equals(other.Level) && Duration.Equals(other.Duration) &&
            StartTime.Equals(other.StartTime) && StackCount == other.StackCount && Inhibited == other.Inhibited &&
            DynamicTag0 == other.DynamicTag0 && DynamicTag1 == other.DynamicTag1 && InstigatorId == other.InstigatorId &&
            PredictionKey == other.PredictionKey;
    }

    /// <summary>부여된 어빌리티 하나의 복제 요약. 언리얼 ActivatableAbilities 복제(소유자 전용)에 해당한다.</summary>
    public struct AbilitySpecRepData : INetworkSerializable, IEquatable<AbilitySpecRepData>
    {
        public int Handle;
        public int AbilityId;
        public short Level;
        public short InputId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Handle);
            serializer.SerializeValue(ref AbilityId);
            serializer.SerializeValue(ref Level);
            serializer.SerializeValue(ref InputId);
        }

        public bool Equals(AbilitySpecRepData other) =>
            Handle == other.Handle && AbilityId == other.AbilityId && Level == other.Level && InputId == other.InputId;
    }

    /// <summary>지속형 게임플레이 큐 하나. 언리얼 ActiveGameplayCues 복제에 해당한다.</summary>
    public struct ActiveCueRepData : INetworkSerializable, IEquatable<ActiveCueRepData>
    {
        public int InstanceId;
        public int TagHash;
        public GameplayCueParameters Parameters;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref InstanceId);
            serializer.SerializeValue(ref TagHash);
            serializer.SerializeValue(ref Parameters);
        }

        public bool Equals(ActiveCueRepData other) => InstanceId == other.InstanceId && TagHash == other.TagHash;
    }

    public partial class AbilitySystemComponent
    {
        private NetworkList<AttributeRepData> _repAttributes;
        private NetworkList<TagCountRepData> _repTags;
        private NetworkList<ActiveEffectRepData> _repEffects;
        private NetworkList<AbilitySpecRepData> _repAbilities;
        private NetworkList<ActiveCueRepData> _repCues;
        private NetworkVariable<int> _replicatedPredictionKey;

        // 서버 상태
        private readonly Dictionary<int, int> _serverReplicatedTagCounts = new Dictionary<int, int>(16);
        private readonly Dictionary<int, ActiveCueRepData> _serverActiveCues = new Dictionary<int, ActiveCueRepData>(8);
        private bool _tagsDirty;
        private bool _effectsDirty;
        private bool _abilitiesDirty;
        private int _nextCueInstanceId;

        // 클라이언트 상태
        private readonly Dictionary<int, int> _clientReplicatedTagCounts = new Dictionary<int, int>(16);
        private readonly Dictionary<int, ActiveCueRepData> _clientActiveCues = new Dictionary<int, ActiveCueRepData>(8);
        private bool _clientAttributesDirty;
        private bool _clientTagsDirty;
        private bool _clientEffectsDirty;
        private bool _clientAbilitiesDirty;
        private bool _clientCuesDirty;

        // 예측
        private readonly Dictionary<int, List<KeyValuePair<AttributeEntry, float>>> _predictedBaseDeltas =
            new Dictionary<int, List<KeyValuePair<AttributeEntry, float>>>();
        private readonly Dictionary<int, List<ActiveGameplayEffect>> _predictedEffects = new Dictionary<int, List<ActiveGameplayEffect>>();
        private readonly HashSet<int> _locallyPredictedKeys = new HashSet<int>();
        private readonly Queue<int> _locallyPredictedKeyOrder = new Queue<int>();
        private int _lastCaughtUpKey;

        private void CreateReplicationLists()
        {
            var effectRead = replicationMode == GameplayEffectReplicationMode.Full
                ? NetworkVariableReadPermission.Everyone
                : NetworkVariableReadPermission.Owner;
            _repAttributes = new NetworkList<AttributeRepData>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            _repTags = new NetworkList<TagCountRepData>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            _repEffects = new NetworkList<ActiveEffectRepData>(null, effectRead, NetworkVariableWritePermission.Server);
            _repAbilities = new NetworkList<AbilitySpecRepData>(null, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
            _repCues = new NetworkList<ActiveCueRepData>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            _replicatedPredictionKey = new NetworkVariable<int>(0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        }

        // ================================================================ 서버

        private void InitializeServerReplication()
        {
            _repAttributes.Clear();
            for (int i = 0; i < _attributes.Count; i++)
            {
                var entry = _attributes[i];
                if (entry.IsMeta) continue;
                entry.RepListIndex = _repAttributes.Count;
                entry.SentBase = entry.Data.BaseValue;
                entry.SentCurrent = entry.Data.CurrentValue;
                _repAttributes.Add(new AttributeRepData { Index = (ushort)entry.Index, Base = entry.SentBase, Current = entry.SentCurrent });
            }
            _tagsDirty = true;
            _effectsDirty = true;
            _abilitiesDirty = true;
        }

        private void FlushServerReplication()
        {
            for (int i = 0; i < _attributes.Count; i++)
            {
                var entry = _attributes[i];
                if (entry.IsMeta || entry.RepListIndex < 0) continue;
                float b = entry.Data.BaseValue;
                float c = entry.Data.CurrentValue;
                if (b.Equals(entry.SentBase) && c.Equals(entry.SentCurrent)) continue;
                entry.SentBase = b;
                entry.SentCurrent = c;
                _repAttributes[entry.RepListIndex] = new AttributeRepData { Index = (ushort)entry.Index, Base = b, Current = c };
            }

            if (_tagsDirty)
            {
                _tagsDirty = false;
                var desired = new List<TagCountRepData>(_serverReplicatedTagCounts.Count);
                foreach (var pair in _serverReplicatedTagCounts)
                    if (pair.Value > 0) desired.Add(new TagCountRepData { TagHash = pair.Key, Count = (short)Mathf.Min(pair.Value, short.MaxValue) });
                desired.Sort((a, b) => a.TagHash.CompareTo(b.TagHash));
                SyncList(_repTags, desired);
            }

            if (_effectsDirty)
            {
                _effectsDirty = false;
                if (replicationMode != GameplayEffectReplicationMode.Minimal)
                {
                    var desired = new List<ActiveEffectRepData>(_activeEffects.Count);
                    for (int i = 0; i < _activeEffects.Count; i++)
                    {
                        var active = _activeEffects[i];
                        if (!active.IsAuthorityOwned || active.IsPendingRemove) continue;
                        var dynamicTags = active.Spec.DynamicGrantedTags;
                        desired.Add(new ActiveEffectRepData
                        {
                            Handle = active.Handle.Id,
                            EffectId = AbilitySystemGlobals.GetAssetId(active.Def),
                            Level = active.Spec.Level,
                            Duration = active.Spec.Duration,
                            StartTime = active.StartWorldTime,
                            StackCount = (byte)Mathf.Clamp(active.Spec.StackCount, 0, 255),
                            Inhibited = active.IsInhibited,
                            DynamicTag0 = dynamicTags.Count > 0 ? dynamicTags[0].Hash : 0,
                            DynamicTag1 = dynamicTags.Count > 1 ? dynamicTags[1].Hash : 0,
                            InstigatorId = active.Spec.Context.InstigatorNetworkObjectId,
                            PredictionKey = active.PredictionKey.Current
                        });
                    }
                    SyncList(_repEffects, desired);
                }
            }

            if (_abilitiesDirty)
            {
                _abilitiesDirty = false;
                var desired = new List<AbilitySpecRepData>(_activatableAbilities.Count);
                for (int i = 0; i < _activatableAbilities.Count; i++)
                {
                    var spec = _activatableAbilities[i];
                    if (spec.PendingRemove) continue;
                    desired.Add(new AbilitySpecRepData
                    {
                        Handle = spec.Handle.Id,
                        AbilityId = AbilitySystemGlobals.GetAssetId(spec.Ability),
                        Level = (short)spec.Level,
                        InputId = (short)spec.InputID
                    });
                }
                SyncList(_repAbilities, desired);
            }
        }

        private static void SyncList<T>(NetworkList<T> list, List<T> desired) where T : unmanaged, IEquatable<T>
        {
            int common = Mathf.Min(list.Count, desired.Count);
            for (int i = 0; i < common; i++)
                if (!list[i].Equals(desired[i])) list[i] = desired[i];
            for (int i = common; i < desired.Count; i++) list.Add(desired[i]);
            for (int i = list.Count - 1; i >= desired.Count; i--) list.RemoveAt(i);
        }

        // ================================================================ 클라이언트

        private void InitializeClientReplication()
        {
            _repAttributes.OnListChanged += _ => _clientAttributesDirty = true;
            _repTags.OnListChanged += _ => _clientTagsDirty = true;
            _repEffects.OnListChanged += _ => _clientEffectsDirty = true;
            _repAbilities.OnListChanged += _ => _clientAbilitiesDirty = true;
            _repCues.OnListChanged += _ => _clientCuesDirty = true;

            _clientAttributesDirty = _clientTagsDirty = _clientEffectsDirty = _clientAbilitiesDirty = _clientCuesDirty = true;
            ProcessClientReplication();
        }

        private void ShutdownReplication()
        {
            _serverReplicatedTagCounts.Clear();
            _serverActiveCues.Clear();
        }

        private void ProcessClientReplication()
        {
            if (!IsSpawned) return;

            if (_clientAttributesDirty)
            {
                _clientAttributesDirty = false;
                for (int i = 0; i < _repAttributes.Count; i++)
                {
                    var data = _repAttributes[i];
                    if (data.Index >= _attributes.Count) continue;
                    var entry = _attributes[data.Index];
                    entry.RepBase = data.Base;
                    entry.RepCurrent = data.Current;
                    entry.HasRepValue = true;
                    UpdateAttributeCurrent(entry);
                }
            }

            if (_clientTagsDirty)
            {
                _clientTagsDirty = false;
                var incoming = new Dictionary<int, int>(_repTags.Count);
                for (int i = 0; i < _repTags.Count; i++) incoming[_repTags[i].TagHash] = _repTags[i].Count;
                var changed = new List<KeyValuePair<int, int>>();
                foreach (var pair in _clientReplicatedTagCounts)
                {
                    incoming.TryGetValue(pair.Key, out int now);
                    if (now != pair.Value) changed.Add(new KeyValuePair<int, int>(pair.Key, now - pair.Value));
                }
                foreach (var pair in incoming)
                    if (!_clientReplicatedTagCounts.ContainsKey(pair.Key)) changed.Add(new KeyValuePair<int, int>(pair.Key, pair.Value));
                _clientReplicatedTagCounts.Clear();
                foreach (var pair in incoming) _clientReplicatedTagCounts[pair.Key] = pair.Value;
                for (int i = 0; i < changed.Count; i++) _ownedTags.UpdateTagCount(GameplayTag.FromHash(changed[i].Key), changed[i].Value);
            }

            if (_clientEffectsDirty)
            {
                _clientEffectsDirty = false;
                RebuildReplicatedEffectProxies();
            }

            if (_clientAbilitiesDirty)
            {
                _clientAbilitiesDirty = false;
                RebuildReplicatedAbilitySpecs();
            }

            if (_clientCuesDirty)
            {
                _clientCuesDirty = false;
                SyncReplicatedCues();
            }
        }

        private void RebuildReplicatedEffectProxies()
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
                if (_activeEffects[i].IsReplicatedProxy) _activeEffects.RemoveAt(i);

            for (int i = 0; i < _repEffects.Count; i++)
            {
                var data = _repEffects[i];
                var def = AbilitySystemGlobals.FindEffectById(data.EffectId);
                if (def == null) continue;
                var spec = new GameplayEffectSpec(def, null, data.Level);
                spec.SetDuration(data.Duration, true);
                spec.StackCount = data.StackCount;
                if (data.DynamicTag0 != 0) spec.DynamicGrantedTags.AddTag(GameplayTag.FromHash(data.DynamicTag0));
                if (data.DynamicTag1 != 0) spec.DynamicGrantedTags.AddTag(GameplayTag.FromHash(data.DynamicTag1));
                var proxy = new ActiveGameplayEffect(new ActiveGameplayEffectHandle(data.Handle), spec, data.StartTime,
                    new PredictionKey(data.PredictionKey), true, true)
                {
                    IsInhibited = data.Inhibited,
                    ReplicatedInstigatorId = data.InstigatorId
                };
                _activeEffects.Add(proxy);
            }
            OnActiveGameplayEffectsChanged?.Invoke();
        }

        private void RebuildReplicatedAbilitySpecs()
        {
            var seen = new HashSet<int>();
            bool changed = false;
            for (int i = 0; i < _repAbilities.Count; i++)
            {
                var data = _repAbilities[i];
                seen.Add(data.Handle);
                var handle = new GameplayAbilitySpecHandle(data.Handle);
                var existing = FindAbilitySpecFromHandle(handle);
                if (existing != null)
                {
                    existing.Level = data.Level;
                    existing.InputID = data.InputId;
                    continue;
                }
                var ability = AbilitySystemGlobals.FindAbilityById(data.AbilityId);
                if (ability == null)
                {
                    Debug.LogWarning($"[GAS] {name}: 어빌리티 ID {data.AbilityId} 를 찾지 못했습니다. AbilitySystemGlobals 에 등록됐는지 확인하세요.", this);
                    continue;
                }
                var spec = new GameplayAbilitySpec(ability, data.Level, data.InputId) { Handle = handle };
                _activatableAbilities.Add(spec);
                if (ability.InstancingPolicy == GameplayAbilityInstancingPolicy.InstancedPerActor) CreateAbilityInstance(spec);
                changed = true;
            }
            for (int i = _activatableAbilities.Count - 1; i >= 0; i--)
            {
                if (seen.Contains(_activatableAbilities[i].Handle.Id)) continue;
                var spec = _activatableAbilities[i];
                spec.PendingRemove = true;
                for (int k = spec.Instances.Count - 1; k >= 0; k--)
                {
                    var instance = spec.Instances[k];
                    if (instance != null && instance.IsActive)
                        instance.EndAbility(spec.Handle, AbilityActorInfo, instance.CurrentActivationInfo, false, true);
                    if (instance != null && instance.IsInstantiated) Destroy(instance);
                }
                spec.Instances.Clear();
                _activatableAbilities.RemoveAt(i);
                changed = true;
            }
            if (changed) OnAbilitiesChanged?.Invoke();
        }

        // ================================================================ 예측

        private void RememberLocallyPredictedKey(PredictionKey key)
        {
            if (!key.IsValidKey || !_locallyPredictedKeys.Add(key.Current)) return;
            _locallyPredictedKeyOrder.Enqueue(key.Current);
            while (_locallyPredictedKeyOrder.Count > 128) _locallyPredictedKeys.Remove(_locallyPredictedKeyOrder.Dequeue());
        }

        /// <summary>클라이언트: Instant 이펙트를 예측 적용한다. Additive 모디파이어만 임시 BaseValue 변화로 얹는다 (비용 차감 등).</summary>
        private void PredictInstantEffect(GameplayEffectSpec spec, PredictionKey key)
        {
            if (!_predictedBaseDeltas.TryGetValue(key.Current, out var list))
                _predictedBaseDeltas[key.Current] = list = new List<KeyValuePair<AttributeEntry, float>>(2);
            var modifiers = spec.Def.Modifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].ModifierOp != GameplayModOp.Additive) continue;
                var entry = FindEntry(modifiers[i].Attribute);
                if (entry == null) continue;
                list.Add(new KeyValuePair<AttributeEntry, float>(entry, spec.GetModifierMagnitude(i, true)));
                UpdateAttributeCurrent(entry);
            }
        }

        private float GetPredictedBaseDelta(AttributeEntry entry)
        {
            if (_predictedBaseDeltas.Count == 0) return 0f;
            float sum = 0f;
            foreach (var pair in _predictedBaseDeltas)
            {
                var list = pair.Value;
                for (int i = 0; i < list.Count; i++)
                    if (list[i].Key == entry) sum += list[i].Value;
            }
            return sum;
        }

        private void RegisterPredictedEffect(PredictionKey key, ActiveGameplayEffect active)
        {
            if (!key.IsValidKey) return;
            if (!_predictedEffects.TryGetValue(key.Current, out var list)) _predictedEffects[key.Current] = list = new List<ActiveGameplayEffect>(2);
            list.Add(active);
        }

        /// <summary>서버가 거절한 예측을 되돌린다 (언리얼 PredictionKey Rejected 델리게이트).</summary>
        private void RejectPrediction(int key)
        {
            ClearPredictedState(key);
            _locallyPredictedKeys.Remove(key);
        }

        /// <summary>서버 상태가 이 키까지 반영됐으면 예측분을 지운다 (언리얼 ReplicatedPredictionKey 캐치업).</summary>
        private void ProcessPredictionCatchUp()
        {
            if (!IsSpawned || !IsOwner) return;
            int replicated = _replicatedPredictionKey.Value;
            if (replicated <= _lastCaughtUpKey) return;
            _lastCaughtUpKey = replicated;

            var keys = new List<int>();
            foreach (var key in _predictedBaseDeltas.Keys) if (key <= replicated) keys.Add(key);
            foreach (var key in _predictedEffects.Keys) if (key <= replicated && !keys.Contains(key)) keys.Add(key);
            for (int i = 0; i < keys.Count; i++) ClearPredictedState(keys[i]);
        }

        private void ClearPredictedState(int key)
        {
            if (_predictedEffects.TryGetValue(key, out var effects))
            {
                _predictedEffects.Remove(key);
                for (int i = 0; i < effects.Count; i++) InternalRemoveActiveGameplayEffect(effects[i], -1);
            }
            if (_predictedBaseDeltas.TryGetValue(key, out var deltas))
            {
                _predictedBaseDeltas.Remove(key);
                for (int i = 0; i < deltas.Count; i++) UpdateAttributeCurrent(deltas[i].Key);
            }
        }

        // ================================================================ 게임플레이 큐

        private void InvokeEffectCues(GameplayEffectSpec spec, GameplayCueEvent eventType, List<GameplayModifierEvaluatedData> evaluated)
        {
            var cues = spec.Def.GameplayCues;
            if (cues.Count == 0) return;
            for (int i = 0; i < cues.Count; i++)
            {
                var cue = cues[i];
                float raw = 0f;
                if (cue.MagnitudeAttribute.IsValid)
                {
                    for (int m = 0; m < evaluated.Count; m++)
                        if (evaluated[m].Attribute == cue.MagnitudeAttribute) raw += evaluated[m].Magnitude;
                }
                else if (evaluated.Count > 0)
                {
                    raw = evaluated[0].Magnitude;
                }
                var parameters = GameplayCueParameters.FromSpec(spec, raw, cue.NormalizeLevel(spec.Level));
                for (int t = 0; t < cue.GameplayCueTags.Count; t++) ExecuteGameplayCueAuthority(cue.GameplayCueTags[t], parameters);
            }
        }

        private void AddEffectCues(ActiveGameplayEffect active)
        {
            var cues = active.Def.GameplayCues;
            for (int i = 0; i < cues.Count; i++)
            {
                var cue = cues[i];
                float raw = active.Def.Modifiers.Count > 0 ? active.Spec.GetModifierMagnitude(0, true) : 0f;
                var parameters = GameplayCueParameters.FromSpec(active.Spec, raw, cue.NormalizeLevel(active.Spec.Level));
                for (int t = 0; t < cue.GameplayCueTags.Count; t++)
                    active.ActiveCueInstanceIds.Add(AddGameplayCue(cue.GameplayCueTags[t], parameters));
            }
        }

        private void RemoveEffectCues(ActiveGameplayEffect active)
        {
            for (int i = 0; i < active.ActiveCueInstanceIds.Count; i++) RemoveGameplayCueByInstance(active.ActiveCueInstanceIds[i]);
            active.ActiveCueInstanceIds.Clear();
        }

        /// <summary>
        /// 언리얼 ExecuteGameplayCue. 서버면 모든 피어에서 재생하고,
        /// 예측 구간의 클라이언트면 자기 화면에서 먼저 재생한다.
        /// </summary>
        public void ExecuteGameplayCue(GameplayTag cueTag, GameplayCueParameters parameters)
        {
            if (IsAuthority)
            {
                ExecuteGameplayCueAuthority(cueTag, parameters);
            }
            else if (ScopedPredictionKey.IsValidKey)
            {
                parameters.PredictionKey = ScopedPredictionKey.Current;
                RememberLocallyPredictedKey(ScopedPredictionKey);
                ExecuteGameplayCueLocal(cueTag, parameters);
            }
        }

        internal void ExecuteGameplayCueFromAbility(GameplayTag cueTag, GameplayCueParameters parameters, GameplayAbility ability)
        {
            if (IsAuthority) ExecuteGameplayCueAuthority(cueTag, parameters);
            else if (ability.IsLocallyControlled()) ExecuteGameplayCueLocal(cueTag, parameters);
        }

        private void ExecuteGameplayCueAuthority(GameplayTag cueTag, GameplayCueParameters parameters)
        {
            ExecuteGameplayCueLocal(cueTag, parameters);
            if (IsSpawned) NetMulticastInvokeGameplayCueExecutedRpc(cueTag.Hash, parameters);
        }

        /// <summary>복제 없이 이 피어에서만 재생한다.</summary>
        public void ExecuteGameplayCueLocal(GameplayTag cueTag, in GameplayCueParameters parameters) =>
            GameplayCueManager.HandleGameplayCue(AbilityActorInfo.AvatarActor, cueTag, GameplayCueEvent.Executed, parameters);

        /// <summary>언리얼 AddGameplayCue. 서버 전용. 지속형 큐를 붙이고 인스턴스 ID 를 돌려준다.</summary>
        public int AddGameplayCue(GameplayTag cueTag, GameplayCueParameters parameters)
        {
            if (!IsAuthority) return 0;
            var data = new ActiveCueRepData { InstanceId = ++_nextCueInstanceId, TagHash = cueTag.Hash, Parameters = parameters };
            _serverActiveCues[data.InstanceId] = data;
            if (IsSpawned) _repCues.Add(data);
            var target = AbilityActorInfo.AvatarActor;
            GameplayCueManager.HandleGameplayCue(target, cueTag, GameplayCueEvent.OnActive, parameters);
            GameplayCueManager.HandleGameplayCue(target, cueTag, GameplayCueEvent.WhileActive, parameters);
            return data.InstanceId;
        }

        /// <summary>언리얼 RemoveGameplayCue. 서버 전용. 태그가 같은 첫 번째 지속형 큐를 뗀다.</summary>
        public void RemoveGameplayCue(GameplayTag cueTag)
        {
            if (!IsAuthority) return;
            foreach (var pair in _serverActiveCues)
            {
                if (pair.Value.TagHash != cueTag.Hash) continue;
                RemoveGameplayCueByInstance(pair.Key);
                return;
            }
        }

        private void RemoveGameplayCueByInstance(int instanceId)
        {
            if (!_serverActiveCues.TryGetValue(instanceId, out var data)) return;
            _serverActiveCues.Remove(instanceId);
            if (IsSpawned)
            {
                for (int i = 0; i < _repCues.Count; i++)
                {
                    if (_repCues[i].InstanceId != instanceId) continue;
                    _repCues.RemoveAt(i);
                    break;
                }
            }
            GameplayCueManager.HandleGameplayCue(AbilityActorInfo.AvatarActor, GameplayTag.FromHash(data.TagHash), GameplayCueEvent.Removed, data.Parameters);
        }

        /// <summary>복제 없이 이 피어에서만 지속형 큐를 붙인다.</summary>
        public void AddGameplayCueLocal(GameplayTag cueTag, in GameplayCueParameters parameters)
        {
            var target = AbilityActorInfo.AvatarActor;
            GameplayCueManager.HandleGameplayCue(target, cueTag, GameplayCueEvent.OnActive, parameters);
            GameplayCueManager.HandleGameplayCue(target, cueTag, GameplayCueEvent.WhileActive, parameters);
        }

        public void RemoveGameplayCueLocal(GameplayTag cueTag, in GameplayCueParameters parameters) =>
            GameplayCueManager.HandleGameplayCue(AbilityActorInfo.AvatarActor, cueTag, GameplayCueEvent.Removed, parameters);

        private void SyncReplicatedCues()
        {
            var incoming = new Dictionary<int, ActiveCueRepData>(_repCues.Count);
            for (int i = 0; i < _repCues.Count; i++) incoming[_repCues[i].InstanceId] = _repCues[i];
            var target = AbilityActorInfo.AvatarActor;

            var removed = new List<int>();
            foreach (var pair in _clientActiveCues)
                if (!incoming.ContainsKey(pair.Key)) removed.Add(pair.Key);
            for (int i = 0; i < removed.Count; i++)
            {
                var data = _clientActiveCues[removed[i]];
                _clientActiveCues.Remove(removed[i]);
                GameplayCueManager.HandleGameplayCue(target, GameplayTag.FromHash(data.TagHash), GameplayCueEvent.Removed, data.Parameters);
            }
            foreach (var pair in incoming)
            {
                if (_clientActiveCues.ContainsKey(pair.Key)) continue;
                _clientActiveCues[pair.Key] = pair.Value;
                var tag = GameplayTag.FromHash(pair.Value.TagHash);
                GameplayCueManager.HandleGameplayCue(target, tag, GameplayCueEvent.OnActive, pair.Value.Parameters);
                GameplayCueManager.HandleGameplayCue(target, tag, GameplayCueEvent.WhileActive, pair.Value.Parameters);
            }
        }

        private void RemoveAllLocalCues()
        {
            var target = AbilityActorInfo.AvatarActor;
            foreach (var pair in _clientActiveCues)
                GameplayCueManager.HandleGameplayCue(target, GameplayTag.FromHash(pair.Value.TagHash), GameplayCueEvent.Removed, pair.Value.Parameters);
            _clientActiveCues.Clear();
            foreach (var pair in _serverActiveCues)
                GameplayCueManager.HandleGameplayCue(target, GameplayTag.FromHash(pair.Value.TagHash), GameplayCueEvent.Removed, pair.Value.Parameters);
            _serverActiveCues.Clear();
        }

        /// <summary>
        /// 언리얼 NetMulticast_InvokeGameplayCueExecuted. 서버 → 다른 모든 피어.
        /// 소유 클라이언트가 같은 예측 키로 이미 재생했다면 건너뛴다.
        /// </summary>
        [Rpc(SendTo.NotServer)]
        private void NetMulticastInvokeGameplayCueExecutedRpc(int cueTagHash, GameplayCueParameters parameters)
        {
            if (IsOwner && parameters.PredictionKey != 0 && _locallyPredictedKeys.Contains(parameters.PredictionKey)) return;
            ExecuteGameplayCueLocal(GameplayTag.FromHash(cueTagHash), parameters);
        }

        // ================================================================ 애니메이션 전파

        /// <summary>
        /// 어빌리티 애니메이션을 이 피어에서 재생하고, 서버라면 다른 피어에도 보낸다
        /// (언리얼 RepAnimMontageInfo 에 해당). 소유 클라이언트가 이미 예측 재생했으면 건너뛴다.
        /// </summary>
        internal void PlayAbilityAnimation(GameplayAbility ability, int triggerHash, int indexParamHash, int index)
        {
            AbilityActorInfo.AnimationDriver?.PlayAbilityAnimation(triggerHash, indexParamHash, index);
            if (!IsAuthority || !IsSpawned) return;
            bool ownerPlaysLocally = ability.NetExecutionPolicy != GameplayAbilityNetExecutionPolicy.ServerOnly;
            PlayAbilityAnimationRpc(triggerHash, indexParamHash, index, ownerPlaysLocally);
        }

        [Rpc(SendTo.NotServer)]
        private void PlayAbilityAnimationRpc(int triggerHash, int indexParamHash, int index, bool ownerPlaysLocally)
        {
            if (ownerPlaysLocally && IsOwner) return;
            AbilityActorInfo.AnimationDriver?.PlayAbilityAnimation(triggerHash, indexParamHash, index);
        }
    }
}
