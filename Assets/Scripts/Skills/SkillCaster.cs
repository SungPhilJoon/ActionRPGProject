using System;
using ActionRPG.Abilities;
using ActionRPG.Combat;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Skills
{
    /// <summary>
    /// 스킬 덱(4칸)과 GAS 를 잇는 컴포넌트.
    ///
    ///  - 서버: 덱(SkillLoadout)이 정해지면 각 칸의 <see cref="SkillDefinition"/> 을 ASC 에 GiveAbility 한다 (입력 슬롯 0~3).
    ///  - 소유자: 입력이 오면 조준 정보(GameplayAbilityTargetData)를 만들어 ASC.AbilityLocalInputPressed 로 넘긴다.
    ///    실제 발동 · 예측 · 서버 검증은 전부 ASC 가 한다.
    ///  - UI: 칸별 스킬 · 남은 쿨다운 · 사용 가능 여부를 ASC 에서 조회해 돌려준다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AbilitySystemComponent))]
    public class SkillCaster : NetworkBehaviour
    {
        [Tooltip("서버가 로드아웃을 지정하지 않았을 때 사용할 기본 덱.")]
        [SerializeField] private SkillDeckPreset fallbackDeck;
        [Tooltip("조준선의 시작 높이.")]
        [SerializeField] private float aimHeight = 1.2f;

        private readonly NetworkVariable<SkillLoadout> _loadout = new NetworkVariable<SkillLoadout>();
        private readonly SkillDefinition[] _slots = new SkillDefinition[GameConstants.SkillSlotCount];
        private readonly GameplayAbilitySpecHandle[] _grantedHandles = new GameplayAbilitySpecHandle[GameConstants.SkillSlotCount];

        private AbilitySystemComponent _asc;
        private Health _health;

        public SkillLoadout Loadout => _loadout.Value;
        public AbilitySystemComponent AbilitySystem => _asc;

        /// <summary>스킬 시전 중인지 (State.Casting 태그).</summary>
        public bool IsCasting => _asc != null && _asc.HasMatchingGameplayTag(GameTags.State_Casting);

        /// <summary>슬롯 구성이 바뀌었을 때. HUD 가 아이콘을 다시 그린다.</summary>
        public event Action LoadoutChanged;

        public Vector3 AimOrigin => transform.position + Vector3.up * aimHeight;

        private void Awake()
        {
            _asc = GetComponent<AbilitySystemComponent>();
            _health = GetComponent<Health>();
        }

        public override void OnNetworkSpawn()
        {
            _loadout.OnValueChanged += OnLoadoutReplicated;
            _asc.OnAbilitiesChanged += OnAbilitiesChanged;
            if (IsServer)
            {
                if (_loadout.Value.IsEmpty && fallbackDeck != null) _loadout.Value = fallbackDeck.ToLoadout();
                GrantLoadoutAbilities();
            }
            ResolveSlots();
        }

        public override void OnNetworkDespawn()
        {
            _loadout.OnValueChanged -= OnLoadoutReplicated;
            if (_asc != null) _asc.OnAbilitiesChanged -= OnAbilitiesChanged;
        }

        private void OnLoadoutReplicated(SkillLoadout _, SkillLoadout __) => ResolveSlots();
        private void OnAbilitiesChanged() => LoadoutChanged?.Invoke();

        // ---------------------------------------------------------------- 서버: 덱 → 어빌리티

        /// <summary>서버 전용. 접속 시 플레이어가 고른 덱을 적용한다.</summary>
        public void ApplyLoadout(in SkillLoadout loadout)
        {
            if (!IsServer) return;
            var db = SkillDatabase.Instance;
            if (db != null && !db.Validate(loadout, out string reason))
            {
                Debug.LogWarning($"[SkillCaster] 잘못된 덱이 들어와 기본 덱으로 대체합니다: {reason}");
                _loadout.Value = fallbackDeck != null ? fallbackDeck.ToLoadout() : default;
            }
            else
            {
                _loadout.Value = loadout.IsEmpty && fallbackDeck != null ? fallbackDeck.ToLoadout() : loadout;
            }
            GrantLoadoutAbilities();
            ResolveSlots();
        }

        /// <summary>서버 전용. 이전 덱의 어빌리티를 회수하고 새 덱의 스킬을 입력 슬롯 0~3 에 부여한다.</summary>
        private void GrantLoadoutAbilities()
        {
            if (!IsServer) return;
            for (int i = 0; i < _grantedHandles.Length; i++)
            {
                if (_grantedHandles[i].IsValid) _asc.ClearAbility(_grantedHandles[i]);
                _grantedHandles[i] = GameplayAbilitySpecHandle.Invalid;
            }

            var db = SkillDatabase.Instance;
            if (db == null) return;
            for (int slot = 0; slot < GameConstants.SkillSlotCount; slot++)
            {
                var skill = db.Get(_loadout.Value[slot]);
                if (skill == null) continue;
                _grantedHandles[slot] = _asc.GiveAbility(new GameplayAbilitySpec(skill, 1, AbilityInputID.Skill1 + slot, this));
            }
        }

        private void ResolveSlots()
        {
            var db = SkillDatabase.Instance;
            for (int i = 0; i < GameConstants.SkillSlotCount; i++)
                _slots[i] = db != null ? db.Get(_loadout.Value[i]) : null;
            LoadoutChanged?.Invoke();
        }

        // ---------------------------------------------------------------- UI 조회

        public SkillDefinition GetSkill(int slot) =>
            slot >= 0 && slot < _slots.Length ? _slots[slot] : null;

        /// <summary>이 칸의 어빌리티 스펙 (서버 · 소유 클라이언트에만 있다).</summary>
        public GameplayAbilitySpec GetSpec(int slot) => _asc != null ? _asc.FindAbilitySpecFromInputID(AbilityInputID.Skill1 + slot) : null;

        public float GetCooldownRemaining(int slot)
        {
            GetCooldown(slot, out float remaining, out _);
            return remaining;
        }

        /// <summary>1 = 막 사용함, 0 = 준비 완료. 쿨다운 UI 를 채우는 데 그대로 쓴다.</summary>
        public float GetCooldownNormalized(int slot)
        {
            GetCooldown(slot, out float remaining, out float duration);
            return duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
        }

        private void GetCooldown(int slot, out float remaining, out float duration)
        {
            remaining = 0f;
            duration = 0f;
            var skill = GetSkill(slot);
            if (skill == null || _asc == null) return;
            var spec = GetSpec(slot);
            var handle = spec != null ? spec.Handle : GameplayAbilitySpecHandle.Invalid;
            skill.GetCooldownTimeRemainingAndDuration(handle, _asc.AbilityActorInfo, out remaining, out duration);
        }

        public bool IsSlotReady(int slot)
        {
            var skill = GetSkill(slot);
            if (skill == null || _asc == null) return false;
            if (GetCooldownRemaining(slot) > 0f) return false;
            if (_asc.HasMatchingGameplayTag(GameTags.State_Debuff_Silence) || _asc.HasMatchingGameplayTag(GameTags.State_Debuff_Stun)) return false;
            return _asc.GetNumericAttribute(ManaSet.ManaAttribute) >= skill.ManaCost;
        }

        // ---------------------------------------------------------------- 시전 (소유자)

        /// <summary>
        /// 소유자가 호출한다. aimDirection 은 수평 조준 방향, groundPoint 는 지면 타게팅 스킬이 쓸 커서 지점.
        /// 조준 정보를 정리해 ASC 에 넘기면 예측 발동 → 서버 검증까지 ASC 가 처리한다.
        /// </summary>
        public void TryCast(int slot, Vector3 aimDirection, Vector3 groundPoint)
        {
            if (!IsOwner || !IsSpawned) return;
            var skill = GetSkill(slot);
            if (skill == null) return;
            if (_health != null && !_health.IsAlive) return;

            aimDirection.y = 0f;
            if (aimDirection.sqrMagnitude < 0.0001f) aimDirection = transform.forward;
            aimDirection.Normalize();

            var targetData = GameplayAbilityTargetData.FromDirection(AimOrigin, aimDirection);

            if (skill.Targeting == SkillTargeting.GroundPoint)
            {
                // 사거리 밖을 찍었으면 사거리 끝으로 당겨서 보낸다.
                if (skill.Range > 0f)
                {
                    Vector3 flat = groundPoint - transform.position;
                    flat.y = 0f;
                    if (flat.magnitude > skill.Range)
                        groundPoint = transform.position + flat.normalized * skill.Range + Vector3.up * (groundPoint.y - transform.position.y);
                }
                targetData.TargetPoint = groundPoint;
                targetData.HasTargetPoint = true;
            }
            else if (skill.Targeting == SkillTargeting.SingleTarget)
            {
                var faction = _health != null ? _health.Faction : Faction.Player;
                var target = ActorRegistry.FindNearestHostile(transform.position, skill.Range, faction);
                if (target == null) return;
                var netObject = target.Transform.GetComponent<NetworkObject>();
                if (netObject == null) return;
                targetData.TargetObjectId = netObject.NetworkObjectId;
                targetData.HasTargetObject = true;
                targetData.TargetPoint = target.Transform.position;
                targetData.HasTargetPoint = true;
            }

            _asc.AbilityLocalInputPressed(AbilityInputID.Skill1 + slot, targetData);
        }

        public void ReleaseCast(int slot)
        {
            if (!IsOwner || _asc == null) return;
            _asc.AbilityLocalInputReleased(AbilityInputID.Skill1 + slot);
        }
    }
}
