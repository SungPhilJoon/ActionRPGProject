using System;
using ActionRPG.Character;
using ActionRPG.Combat;
using ActionRPG.Core;
using ActionRPG.Skills;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Networking
{
    /// <summary>
    /// 플레이어 캐릭터의 네트워크 신원. 이름 표시, 덱 적용, 좌표 이동을 담당한다.
    /// 세션 정보(이름/덱)는 서버가 SessionManager 에서 읽어 여기로 밀어 넣는다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerNetworkIdentity : NetworkBehaviour
    {
        private readonly NetworkVariable<FixedString32Bytes> _displayName = new NetworkVariable<FixedString32Bytes>();
        private readonly NetworkVariable<bool> _ready = new NetworkVariable<bool>();

        private CharacterMotor _motor;
        private SkillCaster _skills;
        private Health _health;

        public string DisplayName => _displayName.Value.ToString();
        public bool IsReady => _ready.Value;

        public event Action<string> NameChanged;
        public event Action<bool> ReadyChanged;

        /// <summary>현재 씬에 스폰된 모든 플레이어. HUD 파티 프레임이 쓴다.</summary>
        public static readonly System.Collections.Generic.List<PlayerNetworkIdentity> All = new System.Collections.Generic.List<PlayerNetworkIdentity>(4);

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _skills = GetComponent<SkillCaster>();
            _health = GetComponent<Health>();
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            _displayName.OnValueChanged += OnNameReplicated;
            _ready.OnValueChanged += OnReadyReplicated;

            if (IsServer)
            {
                ApplySessionData();
                if (_health != null) _health.Died += OnDiedServer;
            }

            NameChanged?.Invoke(DisplayName);
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            _displayName.OnValueChanged -= OnNameReplicated;
            _ready.OnValueChanged -= OnReadyReplicated;
            if (IsServer && _health != null) _health.Died -= OnDiedServer;
        }

        /// <summary>서버 전용. 부활 예약은 스폰 시스템이 맡는다.</summary>
        private void OnDiedServer(DamageInfo _)
        {
            if (PlayerSpawnSystem.Instance != null) PlayerSpawnSystem.Instance.HandlePlayerDeath(this);
        }

        private void OnNameReplicated(FixedString32Bytes _, FixedString32Bytes now) => NameChanged?.Invoke(now.ToString());
        private void OnReadyReplicated(bool _, bool now) => ReadyChanged?.Invoke(now);

        /// <summary>서버 전용. 접속 시 등록해 둔 이름과 덱을 실제 캐릭터에 반영한다.</summary>
        public void ApplySessionData()
        {
            if (!IsServer) return;

            var session = SessionManager.Get(OwnerClientId);
            if (session == null)
            {
                _displayName.Value = new FixedString32Bytes($"플레이어 {OwnerClientId}");
                return;
            }

            // FixedString32Bytes 는 UTF-8 로 29바이트까지만 담긴다. 넘치면 잘라 준다.
            _displayName.Value = Truncate(session.PlayerName, 29);
            _ready.Value = session.IsReady;

            if (_skills != null) _skills.ApplyLoadout(session.Loadout);
        }

        private static FixedString32Bytes Truncate(string value, int maxBytes)
        {
            if (string.IsNullOrEmpty(value)) return new FixedString32Bytes("모험가");

            var bytes = System.Text.Encoding.UTF8.GetByteCount(value);
            while (bytes > maxBytes && value.Length > 1)
            {
                value = value.Substring(0, value.Length - 1);
                bytes = System.Text.Encoding.UTF8.GetByteCount(value);
            }
            return new FixedString32Bytes(value);
        }

        /// <summary>서버 전용. 준비 상태 변경.</summary>
        public void SetReady(bool ready)
        {
            if (!IsServer) return;
            _ready.Value = ready;
            SessionManager.SetReady(OwnerClientId, ready);
        }

        /// <summary>소유자가 준비 버튼을 눌렀을 때.</summary>
        public void RequestToggleReady()
        {
            if (!IsOwner) return;
            ToggleReadyRpc();
        }

        [Rpc(SendTo.Server)]
        private void ToggleReadyRpc() => SetReady(!_ready.Value);

        /// <summary>
        /// 서버 전용. 플레이어를 특정 위치로 옮긴다.
        /// 이동 권한이 소유 클라이언트에 있으므로 서버가 직접 좌표를 쓰면 되돌려진다.
        /// 그래서 소유자에게 텔레포트를 요청한다.
        /// </summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (!IsServer) return;
            TeleportRpc(position, rotation);
        }

        [Rpc(SendTo.Owner)]
        private void TeleportRpc(Vector3 position, Quaternion rotation)
        {
            if (_motor != null) _motor.Teleport(position, rotation);
            else transform.SetPositionAndRotation(position, rotation);
        }
    }
}
