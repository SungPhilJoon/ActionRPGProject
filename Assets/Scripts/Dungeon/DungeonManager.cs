using System;
using System.Collections;
using ActionRPG.Combat;
using ActionRPG.Core;
using ActionRPG.Networking;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Dungeon
{
    public enum DungeonState : byte
    {
        Waiting = 0,
        InProgress = 1,
        Cleared = 2,
        Failed = 3
    }

    /// <summary>
    /// 던전 전체 진행을 관리한다. 방 순서, 클리어/전멸 판정, 결과 화면 트리거.
    /// 판정은 서버, 표시는 전 클라이언트.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class DungeonManager : NetworkBehaviour
    {
        public static DungeonManager Instance { get; private set; }

        [SerializeField] private string dungeonName = "무너진 성채";
        [Tooltip("클리어해야 할 방들. 순서대로 진행한다.")]
        [SerializeField] private DungeonRoom[] rooms = Array.Empty<DungeonRoom>();
        [Tooltip("모든 플레이어가 죽고 이 시간이 지나면 실패 처리한다.")]
        [SerializeField] private float wipeGraceSeconds = 8f;
        [Tooltip("첫 방을 자동으로 시작할지. 끄면 트리거로만 시작한다.")]
        [SerializeField] private bool autoStartFirstRoom;

        private readonly NetworkVariable<DungeonState> _state = new NetworkVariable<DungeonState>(DungeonState.Waiting);
        private readonly NetworkVariable<int> _clearedRooms = new NetworkVariable<int>(0);

        private float _wipeTimer;

        public string DungeonName => dungeonName;
        public DungeonState State => _state.Value;
        public int ClearedRooms => _clearedRooms.Value;
        public int TotalRooms => rooms.Length;

        public event Action<DungeonState> StateChanged;
        public event Action<int, int> ProgressChanged;   // cleared, total

        private void Awake() => Instance = this;

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            _state.OnValueChanged += OnStateReplicated;
            _clearedRooms.OnValueChanged += OnProgressReplicated;

            for (int i = 0; i < rooms.Length; i++)
                if (rooms[i] != null) rooms[i].Cleared += OnRoomCleared;

            if (IsServer)
            {
                _state.Value = DungeonState.InProgress;
                if (autoStartFirstRoom && rooms.Length > 0 && rooms[0] != null) rooms[0].Begin();
            }

            StateChanged?.Invoke(_state.Value);
            ProgressChanged?.Invoke(_clearedRooms.Value, rooms.Length);
        }

        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= OnStateReplicated;
            _clearedRooms.OnValueChanged -= OnProgressReplicated;

            for (int i = 0; i < rooms.Length; i++)
                if (rooms[i] != null) rooms[i].Cleared -= OnRoomCleared;
        }

        private void OnStateReplicated(DungeonState _, DungeonState now)
        {
            StateChanged?.Invoke(now);
            if (now == DungeonState.Cleared) GameEventBus.RaiseDungeonFinished(true);
            else if (now == DungeonState.Failed) GameEventBus.RaiseDungeonFinished(false);
        }

        private void OnProgressReplicated(int _, int now) => ProgressChanged?.Invoke(now, rooms.Length);

        private void OnRoomCleared(DungeonRoom room)
        {
            if (!IsServer) return;

            _clearedRooms.Value = Mathf.Min(_clearedRooms.Value + 1, rooms.Length);

            int index = Array.IndexOf(rooms, room);
            if (index >= 0 && index + 1 < rooms.Length)
            {
                var next = rooms[index + 1];
                // 다음 방은 플레이어가 들어가면 스스로 시작한다. 자동 시작 설정이면 바로 연다.
                if (next != null && autoStartFirstRoom) next.Begin();
            }

            if (_clearedRooms.Value >= rooms.Length) Finish(true);
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer) return;
            if (_state.Value != DungeonState.InProgress) return;

            if (AllPlayersDown())
            {
                _wipeTimer += Time.deltaTime;
                if (_wipeTimer >= wipeGraceSeconds) Finish(false);
            }
            else
            {
                _wipeTimer = 0f;
            }
        }

        private bool AllPlayersDown()
        {
            var players = ActorRegistry.PlayerActors;
            if (players.Count == 0) return false;

            for (int i = 0; i < players.Count; i++)
                if (players[i] != null && players[i].IsAlive) return false;
            return true;
        }

        private void Finish(bool success)
        {
            if (!IsServer || _state.Value == DungeonState.Cleared || _state.Value == DungeonState.Failed) return;
            _state.Value = success ? DungeonState.Cleared : DungeonState.Failed;
        }

        /// <summary>서버 전용. 실패 후 던전을 처음부터 다시 시작한다.</summary>
        public void Restart()
        {
            if (!IsServer) return;

            for (int i = 0; i < rooms.Length; i++)
                if (rooms[i] != null) rooms[i].ResetRoom();

            _clearedRooms.Value = 0;
            _wipeTimer = 0f;
            _state.Value = DungeonState.InProgress;

            StartCoroutine(ReviveAllNextFrame());
        }

        private IEnumerator ReviveAllNextFrame()
        {
            yield return null;
            var spawner = PlayerSpawnSystem.Instance;

            for (int i = 0; i < PlayerNetworkIdentity.All.Count; i++)
            {
                var identity = PlayerNetworkIdentity.All[i];
                if (identity == null) continue;

                if (spawner != null)
                {
                    spawner.GetSpawnPose(out var position, out var rotation);
                    identity.Teleport(position, rotation);
                }

                var health = identity.GetComponent<Health>();
                if (health != null) health.Revive(1f);
            }
        }

        /// <summary>호스트가 던전을 끝내고 로비/메뉴로 돌아갈 때.</summary>
        public void ReturnToMenu()
        {
            if (!IsServer) return;
            if (ConnectionManager.Instance != null) ConnectionManager.Instance.Shutdown();
        }
    }
}
