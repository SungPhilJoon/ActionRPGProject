using ActionRPG.Abilities;
using ActionRPG.Combat;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using ActionRPG.InputSystem;
using ActionRPG.Skills;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Character
{
    /// <summary>
    /// 플레이어 캐릭터의 입력 처리. 소유 클라이언트에서만 동작한다.
    /// 다른 피어에서는 이 컴포넌트가 아무 일도 하지 않고, 모습만 복제된다.
    ///
    /// 공격 · 스킬은 직접 실행하지 않고 ASC 입력 슬롯으로 넘긴다 (언리얼의 Enhanced Input → ASC 바인딩).
    ///  - 평타: <see cref="AbilityInputID.BasicAttack"/>
    ///  - 스킬: <see cref="SkillCaster.TryCast"/> → 슬롯 0~3
    /// 이동 속도는 CombatSet.MoveSpeed 어트리뷰트, 속박 · 기절은 GameplayTag 로 판단한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterMotor))]
    public class PlayerController : NetworkBehaviour
    {
        [SerializeField] private InputReader inputReader;
        [Tooltip("씬에 카메라 리그가 없을 때 생성할 프리팹.")]
        [SerializeField] private GameObject cameraRigPrefab;
        [Tooltip("지면 조준 스킬이 마우스 위치를 쓸지, 화면 중앙을 쓸지.")]
        [SerializeField] private bool aimWithMouse = true;

        private CharacterMotor _motor;
        private AbilitySystemComponent _asc;
        private CharacterAnimatorDriver _animator;
        private SkillCaster _skills;
        private WeaponController _weapons;
        private Health _health;
        private PlayerInteractor _interactor;
        private PlayerCameraRig _cameraRig;
        private bool _controlsBound;

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _asc = GetComponent<AbilitySystemComponent>();
            _animator = GetComponent<CharacterAnimatorDriver>();
            _skills = GetComponent<SkillCaster>();
            _weapons = GetComponent<WeaponController>();
            _health = GetComponent<Health>();
            _interactor = GetComponent<PlayerInteractor>();
            if (inputReader == null) inputReader = GetComponent<InputReader>();
        }

        public override void OnNetworkSpawn()
        {
            if (_asc != null && _asc.HasAttributeSetForAttribute(CombatSet.MoveSpeedAttribute))
            {
                _asc.GetGameplayAttributeValueChangeDelegate(CombatSet.MoveSpeedAttribute).OnValueChanged += OnMoveSpeedChanged;
                _motor.SetBaseSpeed(_asc.GetNumericAttribute(CombatSet.MoveSpeedAttribute));
            }

            if (_health != null)
            {
                _health.Died += OnDied;
                _health.Revived += OnRevived;
            }

            if (!IsOwner)
            {
                // 소유자가 아니면 입력도 카메라도 필요 없다.
                // 모터까지 꺼야 한다. 켜 두면 CharacterController 가 중력을 적용해
                // ClientNetworkTransform 이 복제한 위치와 매 프레임 싸운다.
                if (inputReader != null) inputReader.enabled = false;
                if (_motor != null) _motor.enabled = false;
                enabled = false;
                return;
            }

            ActorRegistry.SetLocalPlayer(gameObject);
            SetupCamera();
            BindInput();
            InputReader.SetCursorLocked(true);
        }

        public override void OnNetworkDespawn()
        {
            if (_asc != null && _asc.HasAttributeSetForAttribute(CombatSet.MoveSpeedAttribute))
                _asc.GetGameplayAttributeValueChangeDelegate(CombatSet.MoveSpeedAttribute).OnValueChanged -= OnMoveSpeedChanged;
            if (_health != null)
            {
                _health.Died -= OnDied;
                _health.Revived -= OnRevived;
            }

            if (!IsOwner) return;
            UnbindInput();
            ActorRegistry.ClearLocalPlayer(gameObject);
            if (_cameraRig != null) _cameraRig.SetTarget(null);
            InputReader.SetCursorLocked(false);
        }

        private void SetupCamera()
        {
            _cameraRig = PlayerCameraRig.Instance;
            if (_cameraRig == null && cameraRigPrefab != null)
            {
                var instance = Instantiate(cameraRigPrefab);
                _cameraRig = instance.GetComponent<PlayerCameraRig>();
                if (_cameraRig == null) _cameraRig = instance.GetComponentInChildren<PlayerCameraRig>();
            }

            if (_cameraRig != null) _cameraRig.SetTarget(transform);
            else Debug.LogWarning("[PlayerController] 카메라 리그를 찾지 못했습니다. 씬에 PlayerCameraRig 를 두거나 프리팹을 지정하세요.");
        }

        private void BindInput()
        {
            if (inputReader == null || _controlsBound) return;
            inputReader.enabled = true;
            inputReader.Jumped += OnJump;
            inputReader.Dodged += OnDodge;
            inputReader.AttackPressed += OnAttack;
            inputReader.AttackReleased += OnAttackReleased;
            inputReader.SkillPressed += OnSkill;
            inputReader.Interacted += OnInteract;
            inputReader.WeaponSwapped += OnSwapWeapon;
            _controlsBound = true;
        }

        private void UnbindInput()
        {
            if (inputReader == null || !_controlsBound) return;
            inputReader.Jumped -= OnJump;
            inputReader.Dodged -= OnDodge;
            inputReader.AttackPressed -= OnAttack;
            inputReader.AttackReleased -= OnAttackReleased;
            inputReader.SkillPressed -= OnSkill;
            inputReader.Interacted -= OnInteract;
            inputReader.WeaponSwapped -= OnSwapWeapon;
            _controlsBound = false;
        }

        private void OnMoveSpeedChanged(OnAttributeChangeData data)
        {
            if (_motor != null) _motor.SetBaseSpeed(data.NewValue);
        }

        private void OnDied(DamageInfo _)
        {
            if (!IsOwner || inputReader == null) return;
            inputReader.EnableGameplay(false);
            if (_motor != null) _motor.SetMoveInput(Vector3.zero);
        }

        private void OnRevived()
        {
            if (!IsOwner || inputReader == null) return;
            inputReader.EnableGameplay(true);
        }

        private bool IsMovementRestricted =>
            _asc != null && (_asc.HasMatchingGameplayTag(GameTags.State_Debuff_Root) || _asc.HasMatchingGameplayTag(GameTags.State_Debuff_Stun));

        private void Update()
        {
            if (!IsOwner || inputReader == null) return;
            if (_health != null && !_health.IsAlive) return;

            UpdateCamera();
            UpdateMovement();
        }

        private void UpdateCamera()
        {
            if (_cameraRig == null) return;
            _cameraRig.AddLookInput(inputReader.Look, Time.deltaTime);
        }

        private void UpdateMovement()
        {
            if (IsMovementRestricted)
            {
                _motor.SetMoveInput(Vector3.zero);
                _motor.SetSprint(false);
                return;
            }

            Vector2 input = inputReader.Move;
            Vector3 world = CameraRelative(input);
            _motor.SetMoveInput(world);
            _motor.SetSprint(inputReader.SprintHeld);
        }

        /// <summary>입력을 카메라 기준 월드 방향으로 바꾼다.</summary>
        private Vector3 CameraRelative(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f) return Vector3.zero;
            Vector3 forward = _cameraRig != null ? _cameraRig.PlanarForward : Vector3.forward;
            Vector3 right = _cameraRig != null ? _cameraRig.PlanarRight : Vector3.right;
            return forward * input.y + right * input.x;
        }

        /// <summary>현재 조준 방향(수평). 이동 중이면 이동 방향, 아니면 카메라 방향.</summary>
        private Vector3 GetAimDirection()
        {
            Vector3 move = CameraRelative(inputReader.Move);
            if (move.sqrMagnitude > 0.01f) return move.normalized;
            if (_cameraRig != null) return _cameraRig.PlanarForward;
            return transform.forward;
        }

        /// <summary>지면 조준 지점. 장판 스킬이 쓴다.</summary>
        private Vector3 GetGroundPoint()
        {
            if (_cameraRig == null) return transform.position + transform.forward * 5f;
            var ray = _cameraRig.GetAimRay(aimWithMouse);
            if (Physics.Raycast(ray, out var hit, 200f, GameConstants.Layers.GroundMask, QueryTriggerInteraction.Ignore))
                return hit.point;
            // 지면을 못 맞히면 캐릭터 높이의 가상 평면에 투영한다.
            var plane = new Plane(Vector3.up, transform.position);
            return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : transform.position + transform.forward * 5f;
        }

        private void OnJump()
        {
            if (IsMovementRestricted) return;
            if (_motor.TryJump())
            {
                if (_animator != null) _animator.PlayJump();
                GameEventBus.RaisePlayerJumped(gameObject);
            }
        }

        private void OnDodge()
        {
            if (IsMovementRestricted) return;
            if (_motor.TryDodge(CameraRelative(inputReader.Move)))
            {
                if (_animator != null) _animator.PlayDodge();
                GameEventBus.RaisePlayerDodged(gameObject);
            }
        }

        private void OnAttack()
        {
            if (_asc == null) return;
            var targetData = GameplayAbilityTargetData.FromDirection(transform.position + Vector3.up, GetAimDirection());
            _asc.AbilityLocalInputPressed(AbilityInputID.BasicAttack, targetData);
        }

        private void OnAttackReleased()
        {
            if (_asc != null) _asc.AbilityLocalInputReleased(AbilityInputID.BasicAttack);
        }

        private void OnSkill(int slot)
        {
            if (_skills == null) return;
            _skills.TryCast(slot, GetAimDirection(), GetGroundPoint());
        }

        private void OnInteract()
        {
            if (_interactor == null) return;
            _interactor.TryInteract();
        }

        private void OnSwapWeapon()
        {
            if (_weapons == null) return;
            _weapons.RequestSwap();
        }
    }
}
