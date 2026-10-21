using System;
using ActionRPG.Core;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Character
{
    /// <summary>
    /// 장착 무기(검/활) 전환과 무기별 기준점(타격 원점, 화살 발사 위치)을 제공한다.
    /// 무기 상태는 서버 권한으로 복제되어 모든 피어가 같은 모델을 보게 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponController : NetworkBehaviour
    {
        [Serializable]
        public struct WeaponVisual
        {
            public WeaponType Type;
            [Tooltip("이 무기를 들었을 때 켜 둘 오브젝트 (검 메시, 활 메시 등).")]
            public GameObject Root;
            [Tooltip("근접 타격 판정의 중심. 비우면 캐릭터 기준으로 계산한다.")]
            public Transform MeleeOrigin;
            [Tooltip("발사체가 나갈 위치. 활에만 필요.")]
            public Transform ProjectileMuzzle;
        }

        [SerializeField] private WeaponType startingWeapon = WeaponType.Sword;
        [SerializeField] private WeaponVisual[] weapons = Array.Empty<WeaponVisual>();
        [Tooltip("무기 전환에 걸리는 시간. 이 동안은 공격할 수 없다.")]
        [SerializeField] private float swapDuration = 0.35f;

        private readonly NetworkVariable<WeaponType> _current = new NetworkVariable<WeaponType>(WeaponType.Sword);
        private CharacterAnimatorDriver _animator;
        private float _swapReadyTime;

        public WeaponType Current => _current.Value;
        public bool IsSwapping => Time.time < _swapReadyTime;
        public event Action<WeaponType> WeaponChanged;

        private void Awake() => _animator = GetComponent<CharacterAnimatorDriver>();

        public override void OnNetworkSpawn()
        {
            _current.OnValueChanged += OnWeaponReplicated;
            if (IsServer) _current.Value = startingWeapon;
            ApplyVisual(_current.Value);
        }

        public override void OnNetworkDespawn() => _current.OnValueChanged -= OnWeaponReplicated;

        private void OnWeaponReplicated(WeaponType _, WeaponType now)
        {
            _swapReadyTime = Time.time + swapDuration;
            ApplyVisual(now);
            WeaponChanged?.Invoke(now);
            GameEventBus.RaisePlayerWeaponChanged(gameObject, now);
        }

        /// <summary>소유자가 호출한다. 실제 변경은 서버가 승인한다.</summary>
        public void RequestSwap()
        {
            if (!IsOwner || IsSwapping) return;
            SwapRpc();
        }

        /// <summary>서버 전용. 특정 무기로 강제 전환 (스킬 요구 무기 등).</summary>
        public void ForceWeapon(WeaponType type)
        {
            if (!IsServer || _current.Value == type) return;
            _current.Value = type;
        }

        [Rpc(SendTo.Server)]
        private void SwapRpc()
        {
            var next = _current.Value == WeaponType.Sword ? WeaponType.Bow : WeaponType.Sword;
            if (!HasWeapon(next)) return;
            _current.Value = next;
        }

        private bool HasWeapon(WeaponType type)
        {
            for (int i = 0; i < weapons.Length; i++)
                if (weapons[i].Type == type) return true;
            return false;
        }

        private void ApplyVisual(WeaponType type)
        {
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i].Root == null) continue;
                weapons[i].Root.SetActive(weapons[i].Type == type);
            }
            if (_animator != null) _animator.SetWeapon(type);
        }

        /// <summary>근접 타격 판정의 원점. 무기별 설정이 없으면 가슴 높이 앞쪽을 쓴다.</summary>
        public Vector3 GetMeleeOrigin()
        {
            var visual = FindVisual(_current.Value);
            if (visual.MeleeOrigin != null) return visual.MeleeOrigin.position;
            return transform.position + Vector3.up * 1.0f;
        }

        /// <summary>발사체 생성 위치.</summary>
        public Vector3 GetMuzzlePosition()
        {
            var visual = FindVisual(_current.Value);
            if (visual.ProjectileMuzzle != null) return visual.ProjectileMuzzle.position;
            return transform.position + Vector3.up * 1.4f + transform.forward * 0.4f;
        }

        private WeaponVisual FindVisual(WeaponType type)
        {
            for (int i = 0; i < weapons.Length; i++)
                if (weapons[i].Type == type) return weapons[i];
            return default;
        }
    }
}
