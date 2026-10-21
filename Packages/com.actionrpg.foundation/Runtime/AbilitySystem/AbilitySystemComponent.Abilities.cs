using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Core.AbilitySystem
{
    public partial class AbilitySystemComponent
    {
        private readonly List<GameplayAbilitySpec> _activatableAbilities = new List<GameplayAbilitySpec>(8);
        private readonly List<GameplayAbility> _activeAbilityInstances = new List<GameplayAbility>(4);
        private readonly List<GameplayAbility> _abilityScratch = new List<GameplayAbility>(4);
        private int _nextAbilityHandle;

        /// <summary>어빌리티가 발동됐을 때 (이 피어에서).</summary>
        public event Action<GameplayAbility> AbilityActivatedCallbacks;
        /// <summary>(어빌리티, 취소 여부) 어빌리티가 끝났을 때 (이 피어에서).</summary>
        public event Action<GameplayAbility, bool> AbilityEndedCallbacks;
        /// <summary>(어빌리티, 실패 사유 태그) 발동에 실패했을 때.</summary>
        public event Action<GameplayAbility, GameplayTagContainer> AbilityFailedCallbacks;
        /// <summary>게임플레이 이벤트가 들어왔을 때 (WaitGameplayEvent 태스크가 구독한다).</summary>
        public event Action<GameplayTag, GameplayEventData> GenericGameplayEvent;
        /// <summary>가진 어빌리티 목록이 바뀌었을 때. 소유 클라이언트에서는 서버 목록이 복제될 때 호출된다.</summary>
        public event Action OnAbilitiesChanged;

        // ================================================================ 부여 / 회수 (서버)

        /// <summary>언리얼 GiveAbility. 서버 전용. 소유 클라이언트에 복제된다.</summary>
        public GameplayAbilitySpecHandle GiveAbility(GameplayAbilitySpec spec)
        {
            if (spec?.Ability == null) return GameplayAbilitySpecHandle.Invalid;
            if (!IsAuthority)
            {
                Debug.LogWarning($"[GAS] {name}: GiveAbility 는 서버에서만 호출할 수 있습니다.", this);
                return GameplayAbilitySpecHandle.Invalid;
            }
            spec.Handle = new GameplayAbilitySpecHandle(++_nextAbilityHandle);
            AddSpecInternal(spec);
            _abilitiesDirty = true;
            return spec.Handle;
        }

        /// <summary>언리얼 ClearAbility. 서버 전용. 실행 중이면 취소한다.</summary>
        public void ClearAbility(GameplayAbilitySpecHandle handle)
        {
            if (!IsAuthority) return;
            var spec = FindAbilitySpecFromHandle(handle);
            if (spec == null) return;
            RemoveSpecInternal(spec);
            _abilitiesDirty = true;
        }

        public void ClearAllAbilities()
        {
            if (!IsAuthority) return;
            for (int i = _activatableAbilities.Count - 1; i >= 0; i--) RemoveSpecInternal(_activatableAbilities[i]);
            _abilitiesDirty = true;
        }

        private void AddSpecInternal(GameplayAbilitySpec spec)
        {
            _activatableAbilities.Add(spec);
            if (spec.Ability.InstancingPolicy == GameplayAbilityInstancingPolicy.InstancedPerActor) CreateAbilityInstance(spec);
            OnAbilitiesChanged?.Invoke();
        }

        private void RemoveSpecInternal(GameplayAbilitySpec spec)
        {
            spec.PendingRemove = true;
            for (int i = spec.Instances.Count - 1; i >= 0; i--)
            {
                var instance = spec.Instances[i];
                if (instance != null && instance.IsActive)
                    instance.CancelAbility(spec.Handle, AbilityActorInfo, instance.CurrentActivationInfo, false);
            }
            for (int i = 0; i < spec.Instances.Count; i++)
                if (spec.Instances[i] != null && spec.Instances[i].IsInstantiated) Destroy(spec.Instances[i]);
            spec.Instances.Clear();
            _activatableAbilities.Remove(spec);
            OnAbilitiesChanged?.Invoke();
        }

        private GameplayAbility CreateAbilityInstance(GameplayAbilitySpec spec)
        {
            AbilitySystemGlobals.SuppressRegistration = true;
            GameplayAbility instance;
            try
            {
                instance = Instantiate(spec.Ability);
            }
            finally
            {
                AbilitySystemGlobals.SuppressRegistration = false;
            }
            instance.name = spec.Ability.name;
            instance.SourceAsset = spec.Ability;
            instance.hideFlags = HideFlags.DontSave;
            spec.Instances.Add(instance);
            return instance;
        }

        // ================================================================ 조회

        public IReadOnlyList<GameplayAbilitySpec> GetActivatableAbilities() => _activatableAbilities;

        public GameplayAbilitySpec FindAbilitySpecFromHandle(GameplayAbilitySpecHandle handle)
        {
            if (!handle.IsValid) return null;
            for (int i = 0; i < _activatableAbilities.Count; i++)
                if (_activatableAbilities[i].Handle == handle) return _activatableAbilities[i];
            return null;
        }

        public GameplayAbilitySpec FindAbilitySpecFromInputID(int inputId)
        {
            if (inputId < 0) return null;
            for (int i = 0; i < _activatableAbilities.Count; i++)
                if (_activatableAbilities[i].InputID == inputId && !_activatableAbilities[i].PendingRemove) return _activatableAbilities[i];
            return null;
        }

        /// <summary>언리얼 FindAbilitySpecFromClass. 같은 에셋으로 부여된 첫 스펙.</summary>
        public GameplayAbilitySpec FindAbilitySpecFromAbility(GameplayAbility ability)
        {
            if (ability == null) return null;
            var asset = ability.AssetDefinition;
            for (int i = 0; i < _activatableAbilities.Count; i++)
                if (_activatableAbilities[i].Ability == asset) return _activatableAbilities[i];
            return null;
        }

        // ================================================================ 발동

        /// <summary>언리얼 TryActivateAbility.</summary>
        public bool TryActivateAbility(GameplayAbilitySpecHandle handle, bool allowRemoteActivation = true) =>
            InternalTryActivateAbility(handle, PredictionKey.None, null, allowRemoteActivation);

        /// <summary>조준 정보 같은 이벤트 데이터를 함께 넘겨 발동한다 (언리얼 TriggerAbilityFromGameplayEvent 와 같은 경로).</summary>
        public bool TryActivateAbility(GameplayAbilitySpecHandle handle, in GameplayEventData eventData, bool allowRemoteActivation = true) =>
            InternalTryActivateAbility(handle, PredictionKey.None, eventData, allowRemoteActivation);

        /// <summary>언리얼 TryActivateAbilitiesByTag. 태그가 맞는 어빌리티를 모두 시도한다.</summary>
        public bool TryActivateAbilitiesByTag(GameplayTagContainer tags, bool allowRemoteActivation = true)
        {
            bool any = false;
            var snapshot = _activatableAbilities.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                var spec = snapshot[i];
                if (spec.Ability.AbilityTags.HasAll(tags) || spec.DynamicAbilityTags.HasAll(tags))
                    any |= TryActivateAbility(spec.Handle, allowRemoteActivation);
            }
            return any;
        }

        public bool TryActivateAbilityByAsset(GameplayAbility ability, bool allowRemoteActivation = true)
        {
            var spec = FindAbilitySpecFromAbility(ability);
            return spec != null && TryActivateAbility(spec.Handle, allowRemoteActivation);
        }

        /// <summary>
        /// 실제 발동 처리. 넷 실행 정책에 따라 로컬 실행, 예측 실행, 서버 요청 중 하나를 고른다.
        /// </summary>
        private bool InternalTryActivateAbility(GameplayAbilitySpecHandle handle, PredictionKey serverPredictionKey,
            GameplayEventData? eventData, bool allowRemoteActivation = true)
        {
            var spec = FindAbilitySpecFromHandle(handle);
            if (spec == null || spec.PendingRemove || spec.Ability == null) return false;

            var ability = spec.Ability;
            var policy = ability.NetExecutionPolicy;
            bool authority = IsAuthority;
            bool local = IsLocallyControlled;

            if (!authority)
            {
                switch (policy)
                {
                    case GameplayAbilityNetExecutionPolicy.ServerOnly:
                    case GameplayAbilityNetExecutionPolicy.ServerInitiated:
                        // 클라이언트는 실행하지 않고 서버에 부탁만 한다.
                        if (!local || !allowRemoteActivation) return false;
                        ServerTryActivateAbilityRpc(handle.Id, spec.InputPressed, 0, eventData.HasValue, eventData ?? default);
                        return true;
                    case GameplayAbilityNetExecutionPolicy.LocalPredicted:
                    case GameplayAbilityNetExecutionPolicy.LocalOnly:
                        if (!local) return false;
                        break;
                }
            }
            else if (policy == GameplayAbilityNetExecutionPolicy.LocalOnly && !local)
            {
                return false;
            }

            var failureTags = new GameplayTagContainer();
            if (!ability.CanActivateAbility(handle, AbilityActorInfo, failureTags))
            {
                AbilityFailedCallbacks?.Invoke(ability, failureTags);
                return false;
            }

            // 실행 모드와 예측 키를 정한다.
            GameplayAbilityActivationMode mode;
            PredictionKey key;
            if (authority)
            {
                mode = GameplayAbilityActivationMode.Authority;
                key = serverPredictionKey;
            }
            else if (policy == GameplayAbilityNetExecutionPolicy.LocalPredicted)
            {
                mode = GameplayAbilityActivationMode.Predicting;
                key = NewPredictionKey();
            }
            else
            {
                mode = GameplayAbilityActivationMode.NonAuthority;
                key = serverPredictionKey;
            }
            var activationInfo = new GameplayAbilityActivationInfo(mode, key);

            // 재발동: 같은 인스턴스가 실행 중이면 먼저 끝낸다.
            if (spec.IsActive && ability.InstancingPolicy == GameplayAbilityInstancingPolicy.InstancedPerActor && ability.bRetriggerInstancedAbility)
            {
                var running = spec.GetPrimaryInstance();
                if (running != null && running.IsActive)
                    running.CancelAbility(handle, AbilityActorInfo, running.CurrentActivationInfo, true);
            }

            GameplayAbility instance = ability.InstancingPolicy switch
            {
                GameplayAbilityInstancingPolicy.NonInstanced => ability,
                GameplayAbilityInstancingPolicy.InstancedPerActor => spec.GetPrimaryInstance() ?? CreateAbilityInstance(spec),
                _ => CreateAbilityInstance(spec)
            };

            spec.ActiveCount++;
            spec.ActivationInfo = activationInfo;
            if (!_activeAbilityInstances.Contains(instance)) _activeAbilityInstances.Add(instance);

            if (mode == GameplayAbilityActivationMode.Predicting)
            {
                RememberLocallyPredictedKey(key);
                ServerTryActivateAbilityRpc(handle.Id, spec.InputPressed, key.Current, eventData.HasValue, eventData ?? default);
            }

            AbilityActivatedCallbacks?.Invoke(instance);

            var previousKey = ScopedPredictionKey;
            ScopedPredictionKey = key;
            try
            {
                instance.CallActivateAbility(handle, AbilityActorInfo, activationInfo, eventData);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                if (instance.IsActive) instance.EndAbility(handle, AbilityActorInfo, activationInfo, true, true);
            }
            finally
            {
                ScopedPredictionKey = previousKey;
            }

            // 서버가 시작하는 어빌리티는 소유 클라이언트에도 실행을 지시한다.
            if (authority && policy == GameplayAbilityNetExecutionPolicy.ServerInitiated && !local && IsSpawned)
                ClientTryActivateAbilityRpc(handle.Id, eventData.HasValue, eventData ?? default);

            return true;
        }

        /// <summary>어빌리티가 EndAbility 에서 호출한다.</summary>
        internal void NotifyAbilityEnded(GameplayAbilitySpecHandle handle, GameplayAbility ability, bool wasCancelled)
        {
            _activeAbilityInstances.Remove(ability);
            var spec = FindAbilitySpecFromHandle(handle);
            if (spec != null)
            {
                spec.ActiveCount = Mathf.Max(0, spec.ActiveCount - 1);
                if (ability.InstancingPolicy == GameplayAbilityInstancingPolicy.InstancedPerExecution)
                {
                    spec.Instances.Remove(ability);
                    if (ability.IsInstantiated) Destroy(ability);
                }
            }
            AbilityEndedCallbacks?.Invoke(ability, wasCancelled);
        }

        private void TickActiveAbilities(float deltaTime)
        {
            if (_activeAbilityInstances.Count == 0) return;
            _abilityScratch.Clear();
            _abilityScratch.AddRange(_activeAbilityInstances);
            for (int i = 0; i < _abilityScratch.Count; i++)
            {
                var ability = _abilityScratch[i];
                if (ability != null && ability.IsActive) ability.TickTasks(deltaTime);
            }
            _abilityScratch.Clear();
        }

        // ================================================================ 입력

        /// <summary>언리얼 AbilityLocalInputPressed. 입력 슬롯에 연결된 어빌리티를 발동하거나, 실행 중이면 입력을 전달한다.</summary>
        public void AbilityLocalInputPressed(int inputId) => AbilityLocalInputPressedInternal(inputId, null);

        /// <summary>조준 정보를 함께 보내는 버전. 발동 시 이벤트 데이터의 TargetData 로 전달된다.</summary>
        public void AbilityLocalInputPressed(int inputId, in GameplayAbilityTargetData targetData) =>
            AbilityLocalInputPressedInternal(inputId, GameplayEventData.WithTargetData(targetData));

        private void AbilityLocalInputPressedInternal(int inputId, GameplayEventData? eventData)
        {
            for (int i = 0; i < _activatableAbilities.Count; i++)
            {
                var spec = _activatableAbilities[i];
                if (spec.InputID != inputId || spec.PendingRemove) continue;
                spec.InputPressed = true;
                if (spec.IsActive)
                {
                    var instance = spec.GetPrimaryInstance() ?? spec.Ability;
                    if (spec.Ability.bReplicateInputDirectly && !IsAuthority) ServerSetInputPressedRpc(spec.Handle.Id);
                    instance.InputPressed(spec.Handle, AbilityActorInfo, spec.ActivationInfo);
                    instance.NotifyInputPressedToTasks();
                }
                else
                {
                    InternalTryActivateAbility(spec.Handle, PredictionKey.None, eventData);
                }
            }
        }

        /// <summary>언리얼 AbilityLocalInputReleased.</summary>
        public void AbilityLocalInputReleased(int inputId)
        {
            for (int i = 0; i < _activatableAbilities.Count; i++)
            {
                var spec = _activatableAbilities[i];
                if (spec.InputID != inputId) continue;
                spec.InputPressed = false;
                if (!spec.IsActive) continue;
                var instance = spec.GetPrimaryInstance() ?? spec.Ability;
                if (spec.Ability.bReplicateInputDirectly && !IsAuthority) ServerSetInputReleasedRpc(spec.Handle.Id);
                instance.InputReleased(spec.Handle, AbilityActorInfo, spec.ActivationInfo);
                instance.NotifyInputReleasedToTasks(0f);
            }
        }

        // ================================================================ 취소

        public void CancelAbilityHandle(GameplayAbilitySpecHandle handle)
        {
            var spec = FindAbilitySpecFromHandle(handle);
            if (spec == null) return;
            CancelSpec(spec, null, replicate: true);
        }

        /// <summary>언리얼 CancelAbilities. withTags 가 맞고 withoutTags 가 없는 실행 중 어빌리티를 취소한다.</summary>
        public void CancelAbilities(GameplayTagContainer withTags, GameplayTagContainer withoutTags = null, GameplayAbility ignore = null)
        {
            var snapshot = _activatableAbilities.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                var spec = snapshot[i];
                if (!spec.IsActive) continue;
                var tags = spec.Ability.AbilityTags;
                bool withMatch = withTags == null || withTags.IsEmpty || tags.HasAny(withTags) || spec.DynamicAbilityTags.HasAny(withTags);
                bool withoutMatch = withoutTags != null && !withoutTags.IsEmpty && (tags.HasAny(withoutTags) || spec.DynamicAbilityTags.HasAny(withoutTags));
                if (withMatch && !withoutMatch) CancelSpec(spec, ignore, replicate: true);
            }
        }

        public void CancelAllAbilities(GameplayAbility ignore = null) => CancelAllAbilities(ignore, true);

        private void CancelAllAbilities(GameplayAbility ignore, bool replicate)
        {
            var snapshot = _activatableAbilities.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
                if (snapshot[i].IsActive) CancelSpec(snapshot[i], ignore, replicate);
        }

        private void CancelSpec(GameplayAbilitySpec spec, GameplayAbility ignore, bool replicate)
        {
            var instances = spec.Ability.InstancingPolicy == GameplayAbilityInstancingPolicy.NonInstanced
                ? new List<GameplayAbility> { spec.Ability }
                : new List<GameplayAbility>(spec.Instances);
            for (int i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                if (instance == null || !instance.IsActive || instance == ignore) continue;
                if (ignore != null && instance.AssetDefinition == ignore.AssetDefinition && instance == ignore) continue;
                instance.CancelAbility(spec.Handle, AbilityActorInfo, instance.CurrentActivationInfo, replicate);
            }
        }

        // ================================================================ 게임플레이 이벤트 / 태그 트리거

        /// <summary>언리얼 HandleGameplayEvent. 이벤트 트리거 어빌리티를 발동하고 대기 중인 태스크에 알린다.</summary>
        public int HandleGameplayEvent(GameplayTag eventTag, GameplayEventData payload)
        {
            int activated = 0;
            payload.EventTag = eventTag;
            var snapshot = _activatableAbilities.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                var spec = snapshot[i];
                var triggers = spec.Ability.AbilityTriggers;
                for (int t = 0; t < triggers.Count; t++)
                {
                    var trigger = triggers[t];
                    if (trigger.TriggerSource != GameplayAbilityTriggerSource.GameplayEvent) continue;
                    if (!eventTag.MatchesTag(trigger.TriggerTag)) continue;
                    if (InternalTryActivateAbility(spec.Handle, PredictionKey.None, payload)) activated++;
                    break;
                }
            }
            GenericGameplayEvent?.Invoke(eventTag, payload);
            return activated;
        }

        private void HandleTagTriggeredAbilities(GameplayTag tag, int count)
        {
            if (_activatableAbilities.Count == 0) return;
            var snapshot = _activatableAbilities.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                var spec = snapshot[i];
                var triggers = spec.Ability.AbilityTriggers;
                for (int t = 0; t < triggers.Count; t++)
                {
                    var trigger = triggers[t];
                    if (trigger.TriggerSource == GameplayAbilityTriggerSource.GameplayEvent) continue;
                    if (!tag.MatchesTagExact(trigger.TriggerTag)) continue;
                    if (count > 0)
                    {
                        if (!spec.IsActive) InternalTryActivateAbility(spec.Handle, PredictionKey.None, null, false);
                    }
                    else if (trigger.TriggerSource == GameplayAbilityTriggerSource.OwnedTagPresent && spec.IsActive)
                    {
                        CancelSpec(spec, null, replicate: true);
                    }
                }
            }
        }

        // ================================================================ 예측 키

        /// <summary>
        /// 언리얼 ScopedPredictionKey. 어빌리티가 발동되는 동안(동기 구간) 유효한 예측 키.
        /// 이 키가 있는 동안 클라이언트는 자기 자신에게 이펙트를 "예측 적용"할 수 있다.
        /// </summary>
        public PredictionKey ScopedPredictionKey { get; private set; }

        private int _nextPredictionKey;

        private PredictionKey NewPredictionKey() => new PredictionKey(++_nextPredictionKey);

        // ================================================================ 종료 전파

        /// <summary>
        /// 언리얼 ReplicateEndOrCancelAbility. 어빌리티가 끝났음을 반대편에 알린다.
        ///  - 예측 중인 클라이언트 → 서버 (ServerEndAbility / ServerCancelAbility)
        ///  - 서버 → 원격 소유 클라이언트 (ClientEndAbility / ClientCancelAbility)
        /// 리슨 서버 호스트 자신의 캐릭터는 반대편이 없으므로 보내지 않는다.
        /// </summary>
        internal void ReplicateEndOrCancelAbility(GameplayAbilitySpecHandle handle, GameplayAbilityActivationInfo activationInfo,
            GameplayAbility ability, bool wasCancelled)
        {
            if (!IsSpawned) return;
            var policy = ability.NetExecutionPolicy;
            if (policy == GameplayAbilityNetExecutionPolicy.LocalOnly) return;

            if (IsAuthority)
            {
                if (IsOwner || policy == GameplayAbilityNetExecutionPolicy.ServerOnly) return;
                ClientEndAbilityRpc(handle.Id, activationInfo.ActivationPredictionKey.Current, wasCancelled);
            }
            else if (IsOwner)
            {
                ServerEndAbilityRpc(handle.Id, activationInfo.ActivationPredictionKey.Current, wasCancelled);
            }
        }

        // ================================================================ RPC

        /// <summary>언리얼 ServerTryActivateAbility. 클라이언트 → 서버. 예측 발동을 검증한다.</summary>
        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void ServerTryActivateAbilityRpc(int handleId, bool inputPressed, int predictionKey, bool hasEventData, GameplayEventData eventData)
        {
            var handle = new GameplayAbilitySpecHandle(handleId);
            var spec = FindAbilitySpecFromHandle(handle);
            if (spec != null) spec.InputPressed = inputPressed;

            bool success = spec != null &&
                           InternalTryActivateAbility(handle, new PredictionKey(predictionKey), hasEventData ? eventData : (GameplayEventData?)null);

            if (predictionKey > 0)
            {
                if (success) ClientActivateAbilitySucceedRpc(handleId, predictionKey);
                else ClientActivateAbilityFailedRpc(handleId, predictionKey);
                if (predictionKey > _replicatedPredictionKey.Value) _replicatedPredictionKey.Value = predictionKey;
            }
        }

        /// <summary>언리얼 ClientActivateAbilitySucceed. 서버가 예측을 승인했다.</summary>
        [Rpc(SendTo.Owner)]
        private void ClientActivateAbilitySucceedRpc(int handleId, int predictionKey)
        {
            var spec = FindAbilitySpecFromHandle(new GameplayAbilitySpecHandle(handleId));
            if (spec == null) return;
            for (int i = 0; i < spec.Instances.Count; i++)
            {
                var instance = spec.Instances[i];
                if (instance != null && instance.IsActive && instance.CurrentActivationInfo.ActivationPredictionKey.Current == predictionKey)
                    instance.SetActivationConfirmed();
            }
        }

        /// <summary>언리얼 ClientActivateAbilityFailed. 서버가 예측을 거절했다. 예측 실행을 되돌린다.</summary>
        [Rpc(SendTo.Owner)]
        private void ClientActivateAbilityFailedRpc(int handleId, int predictionKey)
        {
            var spec = FindAbilitySpecFromHandle(new GameplayAbilitySpecHandle(handleId));
            if (spec != null)
            {
                var instances = new List<GameplayAbility>(spec.Instances);
                if (spec.Ability.InstancingPolicy == GameplayAbilityInstancingPolicy.NonInstanced) instances.Add(spec.Ability);
                for (int i = 0; i < instances.Count; i++)
                {
                    var instance = instances[i];
                    if (instance == null || !instance.IsActive) continue;
                    if (instance.CurrentActivationInfo.ActivationPredictionKey.Current != predictionKey) continue;
                    instance.EndAbility(spec.Handle, AbilityActorInfo, instance.CurrentActivationInfo, false, true);
                }
            }
            RejectPrediction(predictionKey);
        }

        /// <summary>언리얼 ClientTryActivateAbility. ServerInitiated 어빌리티를 소유 클라이언트에서 실행시킨다.</summary>
        [Rpc(SendTo.Owner)]
        private void ClientTryActivateAbilityRpc(int handleId, bool hasEventData, GameplayEventData eventData)
        {
            if (IsAuthority) return;
            var handle = new GameplayAbilitySpecHandle(handleId);
            var spec = FindAbilitySpecFromHandle(handle);
            if (spec == null) return;
            var ability = spec.GetPrimaryInstance() ?? CreateAbilityInstance(spec);
            var info = new GameplayAbilityActivationInfo(GameplayAbilityActivationMode.NonAuthority, PredictionKey.None);
            spec.ActiveCount++;
            spec.ActivationInfo = info;
            if (!_activeAbilityInstances.Contains(ability)) _activeAbilityInstances.Add(ability);
            ability.CallActivateAbility(handle, AbilityActorInfo, info, hasEventData ? eventData : (GameplayEventData?)null);
        }

        /// <summary>언리얼 ServerEndAbility / ServerCancelAbility.</summary>
        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void ServerEndAbilityRpc(int handleId, int predictionKey, bool wasCancelled)
        {
            var spec = FindAbilitySpecFromHandle(new GameplayAbilitySpecHandle(handleId));
            if (spec == null) return;
            if (wasCancelled && !spec.Ability.bServerRespectsRemoteAbilityCancellation) return;
            EndMatchingInstances(spec, predictionKey, wasCancelled);
        }

        /// <summary>언리얼 ClientEndAbility / ClientCancelAbility.</summary>
        [Rpc(SendTo.Owner)]
        private void ClientEndAbilityRpc(int handleId, int predictionKey, bool wasCancelled)
        {
            if (IsAuthority) return;
            var spec = FindAbilitySpecFromHandle(new GameplayAbilitySpecHandle(handleId));
            if (spec != null) EndMatchingInstances(spec, predictionKey, wasCancelled);
        }

        private void EndMatchingInstances(GameplayAbilitySpec spec, int predictionKey, bool wasCancelled)
        {
            var instances = new List<GameplayAbility>(spec.Instances);
            for (int i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                if (instance == null || !instance.IsActive) continue;
                int key = instance.CurrentActivationInfo.ActivationPredictionKey.Current;
                if (predictionKey != 0 && key != 0 && key != predictionKey) continue;
                instance.EndAbility(spec.Handle, AbilityActorInfo, instance.CurrentActivationInfo, false, wasCancelled);
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void ServerSetInputPressedRpc(int handleId)
        {
            var spec = FindAbilitySpecFromHandle(new GameplayAbilitySpecHandle(handleId));
            if (spec == null) return;
            spec.InputPressed = true;
            if (!spec.IsActive) return;
            var instance = spec.GetPrimaryInstance() ?? spec.Ability;
            instance.InputPressed(spec.Handle, AbilityActorInfo, spec.ActivationInfo);
            instance.NotifyInputPressedToTasks();
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void ServerSetInputReleasedRpc(int handleId)
        {
            var spec = FindAbilitySpecFromHandle(new GameplayAbilitySpecHandle(handleId));
            if (spec == null) return;
            spec.InputPressed = false;
            if (!spec.IsActive) return;
            var instance = spec.GetPrimaryInstance() ?? spec.Ability;
            instance.InputReleased(spec.Handle, AbilityActorInfo, spec.ActivationInfo);
            instance.NotifyInputReleasedToTasks(0f);
        }
    }
}
