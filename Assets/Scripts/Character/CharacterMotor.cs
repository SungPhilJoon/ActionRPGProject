using UnityEngine;

namespace ActionRPG.Character
{
    /// <summary>
    /// CharacterController 기반 이동. 네트워크를 전혀 모르며, 누가 입력을 주든 똑같이 움직인다.
    /// 플레이어는 PlayerController 가, 적은 EnemyBrain 이 이 컴포넌트를 조종한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMotor : MonoBehaviour
    {
        [Header("이동")]
        [SerializeField] private float baseMoveSpeed = 5.2f;
        [SerializeField] private float sprintMultiplier = 1.5f;
        [Tooltip("초당 속도 변화량. 클수록 즉각적으로 반응한다.")]
        [SerializeField] private float acceleration = 40f;
        [SerializeField] private float deceleration = 55f;
        [SerializeField] private float rotationSpeed = 900f;

        [Header("점프 / 중력")]
        [SerializeField] private float jumpHeight = 1.4f;
        [SerializeField] private float gravity = -22f;
        [Tooltip("땅에서 떨어진 직후에도 점프를 허용하는 유예 시간.")]
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float groundedStickForce = -3f;

        [Header("회피")]
        [SerializeField] private float dodgeDistance = 4.5f;
        [SerializeField] private float dodgeDuration = 0.32f;
        [SerializeField] private float dodgeCooldown = 0.8f;

        private CharacterController _controller;

        private Vector3 _moveInput;          // 월드 공간 정규화 방향
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _lastGroundedTime;
        private bool _jumpQueued;

        private float _speedMultiplier = 1f;
        private bool _sprinting;
        private bool _movementLocked;
        private bool _rotationLocked;

        private Vector3 _dodgeVelocity;
        private float _dodgeEndTime;
        private float _dodgeReadyTime;

        private Quaternion _targetRotation;

        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public bool IsDodging => Time.time < _dodgeEndTime;
        public bool CanDodge => Time.time >= _dodgeReadyTime && !_movementLocked;
        public bool IsSprinting => _sprinting && _moveInput.sqrMagnitude > 0.01f;
        public Vector3 Velocity => _horizontalVelocity + Vector3.up * _verticalVelocity;

        /// <summary>현재 속도를 최대 속도로 나눈 값. Animator 블렌드에 그대로 쓴다.</summary>
        public float NormalizedSpeed
        {
            get
            {
                float max = CurrentMaxSpeed;
                return max > 0.01f ? Mathf.Clamp01(_horizontalVelocity.magnitude / max) : 0f;
            }
        }

        public float CurrentMaxSpeed => baseMoveSpeed * _speedMultiplier * (_sprinting ? sprintMultiplier : 1f);

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _targetRotation = transform.rotation;
            _lastGroundedTime = -999f;
        }

        /// <summary>CharacterStats 의 MoveSpeed 를 반영할 때 호출.</summary>
        public void SetBaseSpeed(float speed) => baseMoveSpeed = Mathf.Max(0f, speed);

        /// <summary>슬로우/가속 버프 배율.</summary>
        public void SetSpeedMultiplier(float multiplier) => _speedMultiplier = Mathf.Max(0f, multiplier);

        /// <summary>월드 공간 이동 방향(정규화 불필요). 크기가 아날로그 입력 강도로 쓰인다.</summary>
        public void SetMoveInput(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            _moveInput = worldDirection.sqrMagnitude > 1f ? worldDirection.normalized : worldDirection;
        }

        public void SetSprint(bool sprinting) => _sprinting = sprinting;

        /// <summary>스킬 시전 중처럼 이동을 묶어 둘 때. 회피는 별도로 막는다.</summary>
        public void SetMovementLocked(bool locked) => _movementLocked = locked;

        /// <summary>조준 중 캐릭터가 마음대로 돌지 않게 할 때.</summary>
        public void SetRotationLocked(bool locked) => _rotationLocked = locked;

        public void FaceDirection(Vector3 worldDirection, bool instant = false)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f) return;
            _targetRotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
            if (instant) transform.rotation = _targetRotation;
        }

        public bool TryJump()
        {
            if (_movementLocked || IsDodging) return false;
            bool grounded = _controller.isGrounded || Time.time - _lastGroundedTime <= coyoteTime;
            if (!grounded) return false;
            _jumpQueued = true;
            return true;
        }

        /// <summary>방향을 주지 않으면 현재 바라보는 쪽으로 구른다.</summary>
        public bool TryDodge(Vector3 worldDirection = default)
        {
            if (!CanDodge || IsDodging) return false;

            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f)
                worldDirection = _moveInput.sqrMagnitude > 0.01f ? _moveInput : transform.forward;

            worldDirection.Normalize();
            _dodgeVelocity = worldDirection * (dodgeDistance / dodgeDuration);
            _dodgeEndTime = Time.time + dodgeDuration;
            _dodgeReadyTime = Time.time + dodgeCooldown;
            FaceDirection(worldDirection);
            return true;
        }

        /// <summary>돌진 스킬 등 외부에서 강제로 밀어낼 때.</summary>
        public void ForceDash(Vector3 worldDirection, float distance, float duration)
        {
            if (duration <= 0f) return;
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f) worldDirection = transform.forward;
            worldDirection.Normalize();

            _dodgeVelocity = worldDirection * (distance / duration);
            _dodgeEndTime = Time.time + duration;
            FaceDirection(worldDirection);
        }

        /// <summary>넉백. 순간적으로 밀린다.</summary>
        public void ApplyKnockback(Vector3 direction, float force)
        {
            if (force <= 0f) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            _horizontalVelocity += direction.normalized * force;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _targetRotation = rotation;
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _controller.enabled = true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateGrounding();
            UpdateHorizontal(dt);
            UpdateVertical(dt);
            UpdateRotation(dt);

            Vector3 motion = (_horizontalVelocity + Vector3.up * _verticalVelocity) * dt;
            _controller.Move(motion);
        }

        private void UpdateGrounding()
        {
            if (_controller.isGrounded) _lastGroundedTime = Time.time;
        }

        private void UpdateHorizontal(float dt)
        {
            if (IsDodging)
            {
                _horizontalVelocity = _dodgeVelocity;
                return;
            }

            Vector3 desired = _movementLocked ? Vector3.zero : _moveInput * CurrentMaxSpeed;
            float rate = desired.sqrMagnitude > _horizontalVelocity.sqrMagnitude ? acceleration : deceleration;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, desired, rate * dt);
        }

        private void UpdateVertical(float dt)
        {
            if (_controller.isGrounded && _verticalVelocity <= 0f)
                _verticalVelocity = groundedStickForce;

            if (_jumpQueued)
            {
                _jumpQueued = false;
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _lastGroundedTime = -999f;
            }

            _verticalVelocity += gravity * dt;
            _verticalVelocity = Mathf.Max(_verticalVelocity, gravity * 2f); // 종단 속도
        }

        private void UpdateRotation(float dt)
        {
            if (_rotationLocked) return;

            if (!IsDodging && !_movementLocked && _moveInput.sqrMagnitude > 0.01f)
                _targetRotation = Quaternion.LookRotation(_moveInput.normalized, Vector3.up);

            transform.rotation = Quaternion.RotateTowards(transform.rotation, _targetRotation, rotationSpeed * dt);
        }
    }
}
