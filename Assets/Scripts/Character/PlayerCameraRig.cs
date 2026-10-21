using UnityEngine;

namespace ActionRPG.Character
{
    /// <summary>
    /// 3인칭 궤도 카메라. 로컬 플레이어 한 명만 사용하므로 네트워크와 무관하다.
    /// 씬에 하나 두고 PlayerController 가 소유 캐릭터를 대상으로 지정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerCameraRig : MonoBehaviour
    {
        public static PlayerCameraRig Instance { get; private set; }

        [Header("대상")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);

        [Header("거리 / 각도")]
        [SerializeField] private float distance = 6f;
        [SerializeField] private float minPitch = -25f;
        [SerializeField] private float maxPitch = 65f;
        [SerializeField] private float startPitch = 20f;
        [SerializeField] private float sensitivity = 180f;

        [Header("따라가기")]
        [SerializeField] private float positionSmoothTime = 0.06f;
        [SerializeField] private bool invertY;

        [Header("충돌")]
        [SerializeField] private LayerMask collisionLayers = 1;
        [SerializeField] private float collisionRadius = 0.28f;
        [SerializeField] private float minDistance = 1.2f;

        private float _yaw;
        private float _pitch;
        private float _currentDistance;
        private Vector3 _velocity;
        private Camera _camera;

        /// <summary>카메라가 바라보는 수평 방향. 이동 입력을 월드 공간으로 바꿀 때 쓴다.</summary>
        public Vector3 PlanarForward
        {
            get
            {
                Vector3 forward = transform.forward;
                forward.y = 0f;
                return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            }
        }

        public Vector3 PlanarRight
        {
            get
            {
                Vector3 right = transform.right;
                right.y = 0f;
                return right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
            }
        }

        public Camera Camera => _camera;
        public bool HasTarget => target != null;

        private void Awake()
        {
            Instance = this;
            _camera = GetComponent<Camera>();
            if (_camera == null) _camera = GetComponentInChildren<Camera>();
            _pitch = startPitch;
            _currentDistance = distance;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target == null) return;

            _yaw = target.eulerAngles.y;
            _pitch = startPitch;
            SnapToTarget();
        }

        /// <summary>PlayerController 가 매 프레임 Look 입력을 넘겨 준다.</summary>
        public void AddLookInput(Vector2 look, float deltaTime)
        {
            _yaw += look.x * sensitivity * deltaTime;
            _pitch += (invertY ? look.y : -look.y) * sensitivity * deltaTime;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = target.position + targetOffset;

            float desired = ResolveDistance(pivot, rotation);
            _currentDistance = Mathf.Lerp(_currentDistance, desired, 1f - Mathf.Exp(-20f * Time.deltaTime));

            Vector3 desiredPosition = pivot - rotation * Vector3.forward * _currentDistance;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _velocity, positionSmoothTime);
            transform.rotation = rotation;
        }

        /// <summary>벽에 카메라가 박히지 않도록 앞으로 당긴다.</summary>
        private float ResolveDistance(Vector3 pivot, Quaternion rotation)
        {
            Vector3 direction = -(rotation * Vector3.forward);
            if (Physics.SphereCast(pivot, collisionRadius, direction, out var hit, distance, collisionLayers, QueryTriggerInteraction.Ignore))
                return Mathf.Max(minDistance, hit.distance - 0.1f);
            return distance;
        }

        private void SnapToTarget()
        {
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = target.position + targetOffset;
            _currentDistance = distance;
            transform.position = pivot - rotation * Vector3.forward * _currentDistance;
            transform.rotation = rotation;
            _velocity = Vector3.zero;
        }

        /// <summary>화면 중앙(또는 마우스 위치)에서 지면까지 쏘는 조준 레이.</summary>
        public Ray GetAimRay(bool useMousePosition)
        {
            if (_camera == null) return new Ray(transform.position, transform.forward);

            if (useMousePosition && UnityEngine.InputSystem.Mouse.current != null)
            {
                Vector2 mouse = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                return _camera.ScreenPointToRay(mouse);
            }
            return _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }
    }
}
