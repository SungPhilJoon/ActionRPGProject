using System;
using ActionRPG.Abilities;
using ActionRPG.Character;
using ActionRPG.Combat;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using ProtoBuf;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Skills
{
    public enum SkillTargeting : byte
    {
        /// <summary>자기 자신에게. 조준이 필요 없다.</summary>
        Self = 0,
        /// <summary>바라보는 방향으로. 돌진, 부채꼴 베기, 화살 등.</summary>
        Direction = 1,
        /// <summary>커서가 가리키는 바닥 지점. 장판 스킬.</summary>
        GroundPoint = 2,
        /// <summary>가장 가까운 적 하나.</summary>
        SingleTarget = 3
    }

    public enum SkillCategory : byte
    {
        Attack = 0,
        Mobility = 1,
        Support = 2,
        Control = 3,
        Ultimate = 4
    }

    /// <summary>
    /// 스킬 하나. GAS 의 <see cref="GameplayAbility"/> 다.
    ///
    /// 발동 흐름 (LocalPredicted):
    ///   1. 소유 클라이언트가 입력 → ASC 가 예측 키를 만들고 이 어빌리티를 먼저 실행, 서버에 ServerTryActivateAbility.
    ///   2. ActivateAbility 에서 CommitAbility → 마나 비용(GE_Cost_Mana) · 쿨다운(GE_Cooldown_Skill + 스킬별 동적 태그) 적용.
    ///      클라이언트에서는 예측 적용이라 HUD 쿨다운이 즉시 돈다.
    ///   3. 애니메이션(PlayMontageAndWait) · 시전 큐(GameplayCue.Skill.Cast) 재생.
    ///   4. CastTime 뒤 효과 조각을 순서대로 실행: 서버는 Execute(이펙트 적용), 조종 피어는 ExecuteLocal(돌진 등),
    ///      모든 피어는 GameplayCue.Skill.Effect 로 ExecuteVisual.
    ///   5. 서버가 거절하면 소유 클라이언트의 예측 실행 · 비용 · 쿨다운이 되돌려진다.
    ///
    /// 덱 구성 UI 와 네트워크 동기화는 계속 <see cref="Id"/> 를 기준으로 한다.
    /// </summary>
    [CreateAssetMenu(menuName = "ActionRPG/Skills/Skill", fileName = "SK_NewSkill")]
    public class SkillDefinition : GameplayAbility
    {
        [Header("식별")]
        [Tooltip("네트워크로 오가는 고유 ID. 0 은 빈 슬롯을 뜻하므로 1 이상이어야 한다.")]
        public ushort Id = 1;
        public string DisplayName = "New Skill";
        [TextArea(2, 5)] public string Description;
        public Sprite Icon;
        public SkillCategory Category = SkillCategory.Attack;

        [Header("덱")]
        [Tooltip("덱 구성 시 소모하는 코스트. 강한 스킬일수록 높게.")]
        [Min(1)] public int DeckCost = 1;
        [Tooltip("이 무기를 들고 있어야만 쓸 수 있다. Unarmed 면 무기 제한 없음.")]
        public WeaponType RequiredWeapon = WeaponType.Unarmed;
        [Tooltip("시전 시 자동으로 요구 무기로 바꿀지.")]
        public bool AutoSwapWeapon = true;

        [Header("비용 / 쿨다운 (SetByCaller 로 공용 이펙트에 전달)")]
        [Min(0f)] public float Cooldown = 6f;
        [Min(0f)] public float ManaCost = 15f;
        [Tooltip("시전 후 이동이 묶이는 시간.")]
        [Min(0f)] public float MovementLockTime = 0.3f;
        [Tooltip("효과가 실제로 발동하기까지의 선딜.")]
        [Min(0f)] public float CastTime = 0.2f;
        [Tooltip("이 스킬도 글로벌 쿨다운의 영향을 받을지.")]
        public bool UsesGlobalCooldown = true;

        [Header("조준")]
        public SkillTargeting Targeting = SkillTargeting.Direction;
        [Tooltip("사거리. Self 면 무시된다.")]
        [Min(0f)] public float Range = 8f;
        [Tooltip("시전 기준점 높이.")]
        public float AimHeight = 1.2f;

        [Header("효과")]
        [Tooltip("위에서부터 순서대로 실행된다.")]
        public SkillEffect[] Effects = Array.Empty<SkillEffect>();

        [Header("연출")]
        public GameObject CastVfxPrefab;
        public AudioClip CastSfx;

        // ---------------------------------------------------------------- 실행 상태 (인스턴스)

        [NonSerialized] private SkillCastContext _ctx;
        [NonSerialized] private CharacterMotor _lockedMotor;
        [NonSerialized] private GameplayTagContainer _cooldownTags;
        [NonSerialized] private ushort _cooldownTagsForId;

        public GameplayTag CooldownTag => GameTags.SkillCooldown(Id);

        protected override void OnEnable()
        {
            EnsureDefaultTags();
            base.OnEnable();
        }

        /// <summary>스킬이라면 반드시 가져야 할 태그를 채운다 (사망 · 기절 · 침묵 중 발동 불가 등).</summary>
        private void EnsureDefaultTags()
        {
            AbilityTags.AddTag(GameTags.Ability_Skill);
            ActivationOwnedTags.AddTag(GameTags.State_Casting);
            ActivationBlockedTags.AddTag(GameTags.State_Dead);
            ActivationBlockedTags.AddTag(GameTags.State_Debuff_Stun);
            ActivationBlockedTags.AddTag(GameTags.State_Debuff_Silence);
        }

        /// <summary>UI 에 툴팁을 뿌릴 때 쓰는 요약 문자열.</summary>
        public string BuildTooltip()
        {
            return $"{DisplayName}\n{Description}\n쿨다운 {Cooldown:0.#}초 · 마나 {ManaCost:0}";
        }

        private void OnValidate()
        {
            if (Id == 0) Id = 1;
            if (Targeting == SkillTargeting.Self) Range = 0f;
        }

        // ================================================================ 비용 / 쿨다운

        public override GameplayTagContainer GetCooldownTags(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo)
        {
            if (_cooldownTags == null || _cooldownTagsForId != Id)
            {
                _cooldownTags = new GameplayTagContainer(CooldownTag);
                if (UsesGlobalCooldown) _cooldownTags.AddTag(GameTags.Cooldown_Global);
                _cooldownTagsForId = Id;
            }
            return _cooldownTags;
        }

        protected override GameplayEffectSpec MakeCooldownGameplayEffectSpec(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo)
        {
            var effect = CooldownGameplayEffect != null ? CooldownGameplayEffect : ActionRPGGameData.Get()?.SkillCooldownGameplayEffect;
            if (effect == null || Cooldown <= 0f) return null;
            var spec = MakeOutgoingGameplayEffectSpec(handle, actorInfo, effect);
            spec.SetSetByCallerMagnitude(GameTags.SetByCaller_Cooldown, Cooldown);
            spec.DynamicGrantedTags.AddTag(CooldownTag);
            return spec;
        }

        public override void ApplyCooldown(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo, GameplayAbilityActivationInfo activationInfo)
        {
            base.ApplyCooldown(handle, actorInfo, activationInfo);
            var gcd = ActionRPGGameData.Get()?.GlobalCooldownGameplayEffect;
            if (UsesGlobalCooldown && gcd != null)
                ApplyGameplayEffectSpecToOwner(handle, actorInfo, activationInfo, MakeOutgoingGameplayEffectSpec(handle, actorInfo, gcd));
        }

        protected override GameplayEffectSpec MakeCostGameplayEffectSpec(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo)
        {
            var effect = CostGameplayEffect != null ? CostGameplayEffect : ActionRPGGameData.Get()?.ManaCostGameplayEffect;
            if (effect == null || ManaCost <= 0f) return null;
            var spec = MakeOutgoingGameplayEffectSpec(handle, actorInfo, effect);
            spec.SetSetByCallerMagnitude(GameTags.SetByCaller_ManaCost, -ManaCost);
            return spec;
        }

        public override bool CanActivateAbility(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo,
            GameplayTagContainer optionalRelevantTags = null)
        {
            if (!base.CanActivateAbility(handle, actorInfo, optionalRelevantTags)) return false;
            if (RequiredWeapon != WeaponType.Unarmed && !AutoSwapWeapon)
            {
                var weapons = actorInfo.GetAvatarComponent<WeaponController>();
                if (weapons != null && weapons.Current != RequiredWeapon) return false;
            }
            return true;
        }

        // ================================================================ 발동

        protected override void ActivateAbility(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo,
            GameplayAbilityActivationInfo activationInfo, GameplayEventData? triggerEventData)
        {
            var avatar = actorInfo.AvatarActor;
            var targetData = triggerEventData.HasValue && triggerEventData.Value.HasTargetData
                ? triggerEventData.Value.TargetData
                : GameplayAbilityTargetData.FromDirection(avatar.transform.position, avatar.transform.forward);

            if (Targeting == SkillTargeting.SingleTarget && !targetData.HasTargetObject)
            {
                EndAbility(handle, actorInfo, activationInfo, true, true);
                return;
            }

            if (!CommitAbility(handle, actorInfo, activationInfo))
            {
                EndAbility(handle, actorInfo, activationInfo, true, true);
                return;
            }

            _ctx = BuildContext(handle, actorInfo, targetData);

            // 무기 자동 교체는 서버가 결정한다.
            if (HasAuthority() && RequiredWeapon != WeaponType.Unarmed && AutoSwapWeapon)
            {
                var weapons = avatar.GetComponent<WeaponController>();
                if (weapons != null && weapons.Current != RequiredWeapon) weapons.ForceWeapon(RequiredWeapon);
            }

            // 방향 전환 · 이동 잠금은 이동 권한이 있는 피어(조종하는 쪽)만.
            if (IsLocallyControlled())
            {
                var motor = avatar.GetComponent<CharacterMotor>();
                if (motor != null)
                {
                    motor.FaceDirection(_ctx.Direction);
                    if (MovementLockTime > 0f)
                    {
                        motor.SetMovementLocked(true);
                        _lockedMotor = motor;
                        var unlock = AbilityTask_WaitDelay.WaitDelay(this, MovementLockTime);
                        unlock.OnFinish += ReleaseMovementLock;
                        unlock.ReadyForActivation();
                    }
                }
            }

            AbilityTask_PlayMontageAndWait.CreatePlayMontageAndWaitProxy(this, GameConstants.Anim.CastSkill, GameConstants.Anim.SkillIndex,
                _ctx.SlotIndex, 0f).ReadyForActivation();

            ExecuteGameplayCue(GameTags.Cue_Skill_Cast, new GameplayCueParameters
            {
                Location = avatar.transform.position,
                Normal = _ctx.Direction,
                InstigatorId = _ctx.CasterObjectId
            });

            var cast = AbilityTask_WaitDelay.WaitDelay(this, CastTime);
            cast.OnFinish += OnCastFinished;
            cast.ReadyForActivation();

            float maxDelay = 0f;
            for (int i = 0; i < Effects.Length; i++)
                if (Effects[i] != null) maxDelay = Mathf.Max(maxDelay, Effects[i].DelaySeconds);
            var end = AbilityTask_WaitDelay.WaitDelay(this, Mathf.Max(MovementLockTime, CastTime + maxDelay) + 0.05f);
            end.OnFinish += K2_EndAbility;
            end.ReadyForActivation();
        }

        private void OnCastFinished()
        {
            for (int i = 0; i < Effects.Length; i++)
            {
                var effect = Effects[i];
                if (effect == null) continue;
                int index = i;
                if (effect.DelaySeconds > 0f)
                {
                    var wait = AbilityTask_WaitDelay.WaitDelay(this, effect.DelaySeconds);
                    wait.OnFinish += () => RunEffect(index);
                    wait.ReadyForActivation();
                }
                else
                {
                    RunEffect(index);
                }
            }

            if (IsLocallyControlled()) GameEventBus.RaisePlayerSkillCast(GetAvatarActorFromActorInfo(), _ctx.SlotIndex);
        }

        private void RunEffect(int index)
        {
            if (!IsActive || index < 0 || index >= Effects.Length) return;
            var effect = Effects[index];
            if (effect == null) return;

            if (HasAuthority()) effect.Execute(_ctx);
            if (IsLocallyControlled()) effect.ExecuteLocal(_ctx);

            ExecuteGameplayCue(GameTags.Cue_Skill_Effect, new GameplayCueParameters
            {
                Location = _ctx.TargetPoint,
                Normal = _ctx.Direction,
                InstigatorId = _ctx.CasterObjectId,
                UserData = index
            });
        }

        protected override void OnEndAbility(bool wasCancelled) => ReleaseMovementLock();

        private void ReleaseMovementLock()
        {
            if (_lockedMotor == null) return;
            _lockedMotor.SetMovementLocked(false);
            _lockedMotor = null;
        }

        private SkillCastContext BuildContext(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo, in GameplayAbilityTargetData targetData)
        {
            var avatar = actorInfo.AvatarActor;
            var transform = avatar.transform;
            Vector3 direction = targetData.Direction;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
            direction.Normalize();

            var health = avatar.GetComponent<Health>();
            var netObject = avatar.GetComponent<NetworkObject>();
            var spec = actorInfo.AbilitySystemComponent.FindAbilitySpecFromHandle(handle);

            return new SkillCastContext
            {
                Skill = this.AssetDefinition as SkillDefinition ?? this,
                Ability = this,
                SourceASC = actorInfo.AbilitySystemComponent,
                CasterObject = avatar,
                CasterTransform = transform,
                Faction = health != null ? health.Faction : Faction.Neutral,
                CasterObjectId = netObject != null && netObject.IsSpawned ? netObject.NetworkObjectId : 0UL,
                SlotIndex = spec != null ? Mathf.Max(0, spec.InputID) : 0,
                Level = spec?.Level ?? 1,
                Origin = transform.position + Vector3.up * AimHeight,
                Direction = direction,
                TargetPoint = Targeting == SkillTargeting.GroundPoint && targetData.HasTargetPoint
                    ? targetData.TargetPoint
                    : transform.position + direction * Range,
                TargetObjectId = targetData.HasTargetObject ? targetData.TargetObjectId : 0UL
            };
        }

        // ================================================================ 게임플레이 큐 핸들러 (모든 피어)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterCueHandlers()
        {
            GameplayCueManager.RegisterHandler(GameTags.Cue_Skill_Cast, HandleCastCue);
            GameplayCueManager.RegisterHandler(GameTags.Cue_Skill_Effect, HandleEffectCue);
        }

        private static void HandleCastCue(GameObject target, GameplayTag cueTag, GameplayCueEvent eventType, in GameplayCueParameters parameters)
        {
            if (eventType != GameplayCueEvent.Executed) return;
            var skill = AbilitySystemGlobals.FindAbilityById(parameters.SourceObjectId) as SkillDefinition;
            if (skill == null) return;
            var rotation = parameters.Normal.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(parameters.Normal, Vector3.up) : Quaternion.identity;
            if (skill.CastVfxPrefab != null) Destroy(Instantiate(skill.CastVfxPrefab, parameters.Location, rotation), 5f);
            if (skill.CastSfx != null) AudioSource.PlayClipAtPoint(skill.CastSfx, parameters.Location);
        }

        private static void HandleEffectCue(GameObject target, GameplayTag cueTag, GameplayCueEvent eventType, in GameplayCueParameters parameters)
        {
            if (eventType != GameplayCueEvent.Executed) return;
            var skill = AbilitySystemGlobals.FindAbilityById(parameters.SourceObjectId) as SkillDefinition;
            if (skill == null || parameters.UserData < 0 || parameters.UserData >= skill.Effects.Length) return;
            var effect = skill.Effects[parameters.UserData];
            if (effect == null) return;

            var caster = parameters.GetInstigator();
            if (caster == null) caster = target;
            var transform = caster != null ? caster.transform : null;
            var ctx = new SkillCastContext
            {
                Skill = skill,
                CasterObject = caster,
                CasterTransform = transform,
                CasterObjectId = parameters.InstigatorId,
                Level = parameters.AbilityLevel,
                Origin = transform != null ? transform.position + Vector3.up * skill.AimHeight : parameters.Location,
                Direction = parameters.Normal,
                TargetPoint = parameters.Location
            };
            effect.ExecuteVisual(ctx);
        }
    }

    /// <summary>
    /// 플레이어가 게임에 들고 들어가는 스킬 4칸. 네트워크로 그대로 보낼 수 있다.
    /// 0 은 빈 슬롯.
    ///
    /// [ProtoContract]/[ProtoMember] 는 이 구조체를 세이브 데이터(PlayerProfileData) 안에
    /// protobuf-net 으로 중첩 직렬화하기 위해 붙였다. INetworkSerializable 과는 별개의
    /// 관심사라 서로 간섭하지 않는다 — 네트워크 동기화는 Unity.Netcode 가, 디스크 저장은
    /// protobuf-net 이 각자의 방식으로 같은 필드를 읽고 쓴다.
    /// </summary>
    [Serializable, ProtoContract]
    public struct SkillLoadout : INetworkSerializable, IEquatable<SkillLoadout>
    {
        [ProtoMember(1)] public ushort Slot0;
        [ProtoMember(2)] public ushort Slot1;
        [ProtoMember(3)] public ushort Slot2;
        [ProtoMember(4)] public ushort Slot3;

        public ushort this[int index]
        {
            get => index switch { 0 => Slot0, 1 => Slot1, 2 => Slot2, 3 => Slot3, _ => (ushort)0 };
            set
            {
                switch (index)
                {
                    case 0: Slot0 = value; break;
                    case 1: Slot1 = value; break;
                    case 2: Slot2 = value; break;
                    case 3: Slot3 = value; break;
                }
            }
        }

        public bool IsEmpty => Slot0 == 0 && Slot1 == 0 && Slot2 == 0 && Slot3 == 0;

        public int FilledCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < GameConstants.SkillSlotCount; i++) if (this[i] != 0) n++;
                return n;
            }
        }

        /// <summary>같은 스킬이 두 칸에 들어갔는지 확인한다.</summary>
        public bool HasDuplicate()
        {
            for (int i = 0; i < GameConstants.SkillSlotCount; i++)
            {
                if (this[i] == 0) continue;
                for (int j = i + 1; j < GameConstants.SkillSlotCount; j++)
                    if (this[i] == this[j]) return true;
            }
            return false;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Slot0);
            serializer.SerializeValue(ref Slot1);
            serializer.SerializeValue(ref Slot2);
            serializer.SerializeValue(ref Slot3);
        }

        public bool Equals(SkillLoadout other) =>
            Slot0 == other.Slot0 && Slot1 == other.Slot1 && Slot2 == other.Slot2 && Slot3 == other.Slot3;

        public override bool Equals(object obj) => obj is SkillLoadout other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Slot0, Slot1, Slot2, Slot3);
        public override string ToString() => $"[{Slot0}, {Slot1}, {Slot2}, {Slot3}]";
    }
}
