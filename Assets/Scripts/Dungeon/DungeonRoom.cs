using System;
using System.Collections;
using System.Collections.Generic;
using ActionRPG.AI;
using ActionRPG.Combat;
using ActionRPG.Core;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Dungeon
{
    /// <summary>
    /// 던전의 방 하나. 플레이어가 들어오면 웨이브를 소환하고, 전멸시키면 문을 연다.
    /// 소환과 클리어 판정은 서버에서만 한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class DungeonRoom : NetworkBehaviour
    {
        [Serializable]
        public class SpawnEntry
        {
            public GameObject EnemyPrefab;
            [Tooltip("비워 두면 프리팹에 설정된 정의를 그대로 쓴다.")]
            public EnemyDefinition OverrideDefinition;
            [Min(1)] public int Count = 1;
            [Tooltip("이 항목이 사용할 스폰 지점 인덱스. 비우면 전체에서 순환한다.")]
            public int[] SpawnPointIndices;
        }

        [Serializable]
        public class SpawnWave
        {
            public string Name = "웨이브 1";
            [Tooltip("이전 웨이브가 끝나고 이만큼 뒤에 나온다.")]
            [Min(0f)] public float DelaySeconds = 1f;
            public SpawnEntry[] Entries = Array.Empty<SpawnEntry>();
        }

        [Header("식별")]
        [SerializeField] private string roomId = "Room_01";
        [Tooltip("보스 방이면 별도 UI 를 띄운다.")]
        [SerializeField] private bool isBossRoom;

        [Header("전투")]
        [SerializeField] private SpawnWave[] waves = Array.Empty<SpawnWave>();
        [SerializeField] private Transform[] spawnPoints;

        [Header("문")]
        [Tooltip("전투가 시작되면 닫히고, 클리어하면 열린다.")]
        [SerializeField] private DungeonDoor[] entranceDoors;
        [SerializeField] private DungeonDoor[] exitDoors;

        [Header("진행")]
        [Tooltip("플레이어가 트리거에 들어오면 자동으로 시작할지. 끄면 DungeonManager 가 시작시킨다.")]
        [SerializeField] private bool startOnTriggerEnter = true;
        [Tooltip("클리어 후 이 지점이 새 부활 위치가 된다.")]
        [SerializeField] private Transform checkpoint;

        private readonly NetworkVariable<bool> _active = new NetworkVariable<bool>(false);
        private readonly NetworkVariable<bool> _cleared = new NetworkVariable<bool>(false);
        private readonly NetworkVariable<int> _waveIndex = new NetworkVariable<int>(-1);

        private readonly List<Health> _aliveEnemies = new List<Health>(16);
        private int _spawnCursor;
        private Coroutine _runner;

        public string RoomId => roomId;
        public bool IsBossRoom => isBossRoom;
        public bool IsActive => _active.Value;
        public bool IsCleared => _cleared.Value;
        public int CurrentWave => _waveIndex.Value;
        public int WaveCount => waves.Length;
        public int AliveEnemyCount => _aliveEnemies.Count;

        /// <summary>서버에서 방이 클리어될 때. DungeonManager 가 구독한다.</summary>
        public event Action<DungeonRoom> Cleared;

        public override void OnNetworkSpawn()
        {
            _active.OnValueChanged += OnActiveReplicated;
            _cleared.OnValueChanged += OnClearedReplicated;
        }

        public override void OnNetworkDespawn()
        {
            _active.OnValueChanged -= OnActiveReplicated;
            _cleared.OnValueChanged -= OnClearedReplicated;
        }

        private void OnActiveReplicated(bool _, bool now)
        {
            if (now) GameEventBus.RaiseDungeonRoomStarted(roomId);
        }

        private void OnClearedReplicated(bool _, bool now)
        {
            if (now) GameEventBus.RaiseDungeonRoomCleared(roomId);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsSpawned || !IsServer || !startOnTriggerEnter) return;
            if (_active.Value || _cleared.Value) return;

            var damageable = CombatQuery.Resolve(other);
            if (damageable == null || damageable.Faction != Faction.Player) return;

            Begin();
        }

        /// <summary>서버 전용. 전투를 시작한다.</summary>
        public void Begin()
        {
            if (!IsServer || _active.Value || _cleared.Value) return;

            _active.Value = true;
            SetDoors(entranceDoors, false);
            SetDoors(exitDoors, false);

            if (waves.Length == 0)
            {
                Complete();
                return;
            }

            _runner = StartCoroutine(RunWaves());
        }

        private IEnumerator RunWaves()
        {
            for (int i = 0; i < waves.Length; i++)
            {
                var wave = waves[i];
                if (wave.DelaySeconds > 0f) yield return new WaitForSeconds(wave.DelaySeconds);
                if (!IsSpawned) yield break;

                _waveIndex.Value = i;
                SpawnWaveEnemies(wave);

                // 이 웨이브가 전멸할 때까지 대기.
                while (_aliveEnemies.Count > 0)
                {
                    PruneDeadEnemies();
                    yield return new WaitForSeconds(0.25f);
                    if (!IsSpawned) yield break;
                }
            }

            Complete();
        }

        private void SpawnWaveEnemies(SpawnWave wave)
        {
            for (int e = 0; e < wave.Entries.Length; e++)
            {
                var entry = wave.Entries[e];
                if (entry == null || entry.EnemyPrefab == null) continue;

                for (int n = 0; n < entry.Count; n++)
                {
                    Transform point = PickSpawnPoint(entry, n);
                    Vector3 position = point != null ? point.position : transform.position;
                    Quaternion rotation = point != null ? point.rotation : transform.rotation;

                    var instance = Instantiate(entry.EnemyPrefab, position, rotation);
                    var netObject = instance.GetComponent<NetworkObject>();
                    if (netObject == null)
                    {
                        Debug.LogError($"[DungeonRoom] {entry.EnemyPrefab.name} 에 NetworkObject 가 없습니다.", entry.EnemyPrefab);
                        Destroy(instance);
                        continue;
                    }

                    if (entry.OverrideDefinition != null)
                    {
                        var brain = instance.GetComponent<EnemyBrain>();
                        if (brain != null) brain.Configure(entry.OverrideDefinition);
                    }

                    netObject.Spawn(true);

                    var health = instance.GetComponent<Health>();
                    if (health != null) _aliveEnemies.Add(health);
                }
            }
        }

        private Transform PickSpawnPoint(SpawnEntry entry, int index)
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return null;

            if (entry.SpawnPointIndices != null && entry.SpawnPointIndices.Length > 0)
            {
                int i = entry.SpawnPointIndices[index % entry.SpawnPointIndices.Length];
                return i >= 0 && i < spawnPoints.Length ? spawnPoints[i] : spawnPoints[0];
            }

            return spawnPoints[_spawnCursor++ % spawnPoints.Length];
        }

        private void PruneDeadEnemies()
        {
            for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
            {
                var health = _aliveEnemies[i];
                if (health == null || !health.IsAlive) _aliveEnemies.RemoveAt(i);
            }
        }

        private void Complete()
        {
            if (!IsServer || _cleared.Value) return;

            _cleared.Value = true;
            _active.Value = false;
            SetDoors(entranceDoors, true);
            SetDoors(exitDoors, true);

            if (checkpoint != null && Networking.PlayerSpawnSystem.Instance != null)
                Networking.PlayerSpawnSystem.Instance.ActiveCheckpoint = checkpoint;

            Cleared?.Invoke(this);
        }

        /// <summary>서버 전용. 던전을 다시 시작할 때 초기 상태로 되돌린다.</summary>
        public void ResetRoom()
        {
            if (!IsServer) return;
            if (_runner != null) StopCoroutine(_runner);

            for (int i = 0; i < _aliveEnemies.Count; i++)
            {
                var health = _aliveEnemies[i];
                if (health == null) continue;
                var netObject = health.GetComponent<NetworkObject>();
                if (netObject != null && netObject.IsSpawned) netObject.Despawn();
            }

            _aliveEnemies.Clear();
            _active.Value = false;
            _cleared.Value = false;
            _waveIndex.Value = -1;
            _spawnCursor = 0;
        }

        private void SetDoors(DungeonDoor[] doors, bool open)
        {
            if (doors == null) return;
            for (int i = 0; i < doors.Length; i++)
                if (doors[i] != null) doors[i].SetOpen(open);
        }

        private void OnDrawGizmosSelected()
        {
            if (spawnPoints == null) return;
            Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.8f);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] == null) continue;
                Gizmos.DrawWireSphere(spawnPoints[i].position, 0.5f);
            }

            if (checkpoint != null)
            {
                Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.8f);
                Gizmos.DrawWireCube(checkpoint.position, Vector3.one);
            }
        }
    }
}
