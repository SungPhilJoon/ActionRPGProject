using ActionRPG.Combat;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Character
{
    /// <summary>
    /// 모터/전투 상태를 Animator 파라미터로 옮긴다.
    /// 파라미터가 없는 컨트롤러여도 경고 없이 동작하도록 존재 여부를 미리 확인한다.
    /// 이동 관련 값은 각 피어가 로컬에서 계산하고, 단발성 트리거만 RPC 로 맞춘다.
    ///
    /// GAS 어빌리티의 PlayMontageAndWait 태스크는 <see cref="IAbilityAnimationDriver"/> 로 이 컴포넌트를 부르고,
    /// 다른 피어로의 전파는 ASC 가 맡는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterAnimatorDriver : NetworkBehaviour, IAbilityAnimationDriver
    {
        /// <summary>IAbilityAnimationDriver. 이 피어에서만 재생한다 (전파는 ASC 가 한다).</summary>
        public void PlayAbilityAnimation(int triggerHash, int indexParamHash, int index)
        {
            if (indexParamHash != 0) SafeSetInt(indexParamHash, index);
            SafeTrigger(triggerHash);
        }

        [SerializeField] private Animator animator;
        [SerializeField] private float parameterDamping = 0.12f;

        private CharacterMotor _motor;
        private Health _health;
        private bool _hasSpeed, _hasGrounded, _hasSprinting, _hasWeapon, _hasMoveX, _hasMoveY;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _motor = GetComponent<CharacterMotor>();
            _health = GetComponent<Health>();
            CacheParameters();
        }

        private void CacheParameters()
        {
            if (animator == null) return;
            foreach (var p in animator.parameters)
            {
                if (p.nameHash == GameConstants.Anim.Speed) _hasSpeed = true;
                else if (p.nameHash == GameConstants.Anim.Grounded) _hasGrounded = true;
                else if (p.nameHash == GameConstants.Anim.Sprinting) _hasSprinting = true;
                else if (p.nameHash == GameConstants.Anim.WeaponType) _hasWeapon = true;
                else if (p.nameHash == GameConstants.Anim.MoveX) _hasMoveX = true;
                else if (p.nameHash == GameConstants.Anim.MoveY) _hasMoveY = true;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (_health != null)
            {
                _health.Died += OnDied;
                _health.Revived += OnRevived;
                _health.Damaged += OnDamaged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_health != null)
            {
                _health.Died -= OnDied;
                _health.Revived -= OnRevived;
                _health.Damaged -= OnDamaged;
            }
        }

        private void Update()
        {
            if (animator == null || _motor == null) return;

            if (_hasSpeed) animator.SetFloat(GameConstants.Anim.Speed, _motor.NormalizedSpeed, parameterDamping, Time.deltaTime);
            if (_hasGrounded) animator.SetBool(GameConstants.Anim.Grounded, _motor.IsGrounded);
            if (_hasSprinting) animator.SetBool(GameConstants.Anim.Sprinting, _motor.IsSprinting);

            if (_hasMoveX || _hasMoveY)
            {
                Vector3 local = transform.InverseTransformDirection(_motor.Velocity);
                float max = Mathf.Max(0.01f, _motor.CurrentMaxSpeed);
                if (_hasMoveX) animator.SetFloat(GameConstants.Anim.MoveX, local.x / max, parameterDamping, Time.deltaTime);
                if (_hasMoveY) animator.SetFloat(GameConstants.Anim.MoveY, local.z / max, parameterDamping, Time.deltaTime);
            }
        }

        public void SetWeapon(WeaponType weapon)
        {
            if (animator != null && _hasWeapon) animator.SetInteger(GameConstants.Anim.WeaponType, (int)weapon);
        }

        /// <summary>서버 또는 소유자가 호출하면 모든 피어에서 같은 애니메이션이 재생된다.</summary>
        public void PlayAttack(int comboIndex)
        {
            if (!IsSpawned) { LocalAttack(comboIndex); return; }
            BroadcastAttackRpc(comboIndex);
        }

        public void PlaySkill(int slotIndex)
        {
            if (!IsSpawned) { LocalSkill(slotIndex); return; }
            BroadcastSkillRpc(slotIndex);
        }

        public void PlayDodge()
        {
            if (!IsSpawned) { SafeTrigger(GameConstants.Anim.Dodge); return; }
            BroadcastDodgeRpc();
        }

        public void PlayJump()
        {
            if (!IsSpawned) { SafeTrigger(GameConstants.Anim.Jump); return; }
            BroadcastJumpRpc();
        }

        [Rpc(SendTo.Everyone, RequireOwnership = false)]
        private void BroadcastAttackRpc(int comboIndex) => LocalAttack(comboIndex);

        [Rpc(SendTo.Everyone, RequireOwnership = false)]
        private void BroadcastSkillRpc(int slotIndex) => LocalSkill(slotIndex);

        [Rpc(SendTo.Everyone, RequireOwnership = false)]
        private void BroadcastDodgeRpc() => SafeTrigger(GameConstants.Anim.Dodge);

        [Rpc(SendTo.Everyone, RequireOwnership = false)]
        private void BroadcastJumpRpc() => SafeTrigger(GameConstants.Anim.Jump);

        /// <summary>이 피어에서만 재생. 상위 시스템이 이미 자체 RPC 로 동기화한 경우에 쓴다.</summary>
        public void PlayAttackLocal(int comboIndex) => LocalAttack(comboIndex);

        /// <summary>이 피어에서만 재생.</summary>
        public void PlaySkillLocal(int slotIndex) => LocalSkill(slotIndex);

        /// <summary>이 피어에서만 재생.</summary>
        public void PlayDodgeLocal() => SafeTrigger(GameConstants.Anim.Dodge);

        private void LocalAttack(int comboIndex)
        {
            SafeSetInt(GameConstants.Anim.AttackIndex, comboIndex);
            SafeTrigger(GameConstants.Anim.Attack);
        }

        private void LocalSkill(int slotIndex)
        {
            SafeSetInt(GameConstants.Anim.SkillIndex, slotIndex);
            SafeTrigger(GameConstants.Anim.CastSkill);
        }

        private void OnDamaged(DamageInfo _) => SafeTrigger(GameConstants.Anim.Hit);
        private void OnDied(DamageInfo _) => SafeTrigger(GameConstants.Anim.Die);
        private void OnRevived() => SafeTrigger(GameConstants.Anim.Revive);

        private void SafeTrigger(int hash)
        {
            if (animator == null || !HasParameter(hash, AnimatorControllerParameterType.Trigger)) return;
            animator.SetTrigger(hash);
        }

        private void SafeSetInt(int hash, int value)
        {
            if (animator == null || !HasParameter(hash, AnimatorControllerParameterType.Int)) return;
            animator.SetInteger(hash, value);
        }

        private bool HasParameter(int hash, AnimatorControllerParameterType type)
        {
            var parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
                if (parameters[i].nameHash == hash && parameters[i].type == type) return true;
            return false;
        }
    }
}
