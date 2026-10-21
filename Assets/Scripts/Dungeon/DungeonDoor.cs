using ActionRPG.Core;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Dungeon
{
    /// <summary>
    /// 방을 막는 문. 열림/닫힘 상태만 복제하고 실제 연출은 각 피어가 보간으로 처리한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class DungeonDoor : NetworkBehaviour, IInteractable
    {
        [SerializeField] private Transform movingPart;
        [Tooltip("열렸을 때의 로컬 위치 오프셋.")]
        [SerializeField] private Vector3 openOffset = new Vector3(0f, 4f, 0f);
        [SerializeField] private float moveDuration = 1.2f;
        [SerializeField] private bool startOpen = true;

        [Header("상호작용")]
        [Tooltip("플레이어가 직접 열 수 있는 문인지. 전투 잠금 문은 꺼 둔다.")]
        [SerializeField] private bool playerOperable;
        [SerializeField] private string prompt = "문 열기";

        private readonly NetworkVariable<bool> _open = new NetworkVariable<bool>(true);

        private Vector3 _closedLocalPosition;
        private float _progress;
        private Collider _blocker;

        public bool IsOpen => _open.Value;
        public string Prompt => prompt;

        private void Awake()
        {
            if (movingPart == null) movingPart = transform;
            _closedLocalPosition = movingPart.localPosition;
            _blocker = GetComponent<Collider>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) _open.Value = startOpen;

            _progress = _open.Value ? 1f : 0f;
            ApplyProgress(true);
        }

        /// <summary>서버 전용.</summary>
        public void SetOpen(bool open)
        {
            if (!IsServer) return;
            _open.Value = open;
        }

        private void Update()
        {
            float target = _open.Value ? 1f : 0f;
            if (Mathf.Approximately(_progress, target)) return;

            float speed = moveDuration > 0f ? Time.deltaTime / moveDuration : 1f;
            _progress = Mathf.MoveTowards(_progress, target, speed);
            ApplyProgress(false);
        }

        private void ApplyProgress(bool instant)
        {
            movingPart.localPosition = _closedLocalPosition + openOffset * _progress;
            if (_blocker != null) _blocker.isTrigger = _progress > 0.9f;
            if (instant) { /* 첫 프레임에 스냅 */ }
        }

        public bool CanInteract(GameObject interactor) => playerOperable && !_open.Value;

        public void Interact(GameObject interactor)
        {
            if (!playerOperable) return;
            OpenRpc();
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void OpenRpc()
        {
            if (!playerOperable) return;
            _open.Value = true;
        }
    }
}
