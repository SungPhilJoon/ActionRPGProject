using System;
using ActionRPG.Core;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Character
{
    /// <summary>
    /// 주변의 상호작용 대상을 찾아 UI 에 알리고, 입력이 오면 실행한다.
    /// 탐색은 소유 클라이언트에서, 실제 처리는 대상 구현이 알아서 한다
    /// (서버 권한이 필요한 대상은 스스로 RPC 를 쏘면 된다).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInteractor : NetworkBehaviour
    {
        [SerializeField] private float radius = 2.2f;
        [SerializeField] private Vector3 originOffset = new Vector3(0f, 1f, 0f);
        [SerializeField] private float refreshInterval = 0.15f;

        private readonly Collider[] _buffer = new Collider[8];
        private IInteractable _focus;
        private float _nextRefresh;

        /// <summary>현재 조준 중인 대상. 없으면 null. UI 프롬프트가 구독한다.</summary>
        public IInteractable Focus => _focus;
        public event Action<IInteractable> FocusChanged;

        private void Update()
        {
            if (!IsOwner || Time.time < _nextRefresh) return;
            _nextRefresh = Time.time + refreshInterval;
            RefreshFocus();
        }

        private void RefreshFocus()
        {
            Vector3 origin = transform.position + originOffset;
            int count = Physics.OverlapSphereNonAlloc(origin, radius, _buffer, GameConstants.Layers.InteractableMask, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < count; i++)
            {
                var candidate = _buffer[i].GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.CanInteract(gameObject)) continue;

                // 캐릭터가 바라보는 쪽에 가까울수록 우선.
                Vector3 toTarget = _buffer[i].transform.position - origin;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 0.0001f) { best = candidate; break; }

                float score = Vector3.Dot(transform.forward, toTarget.normalized) - toTarget.magnitude * 0.1f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (!ReferenceEquals(best, _focus))
            {
                _focus = best;
                FocusChanged?.Invoke(_focus);
            }
        }

        public void TryInteract()
        {
            if (!IsOwner) return;
            RefreshFocus();
            if (_focus == null || !_focus.CanInteract(gameObject)) return;

            _focus.Interact(gameObject);
            GameEventBus.RaisePlayerInteracted(gameObject, _focus);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position + originOffset, radius);
        }
    }
}
