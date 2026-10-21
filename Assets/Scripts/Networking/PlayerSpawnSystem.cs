using System.Collections;
using System.Collections.Generic;
using ActionRPG.Combat;
using ActionRPG.Core;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Networking
{
    /// <summary>
    /// 게임플레이 씬에 하나 두는 스폰 관리자.
    /// NetworkManager 가 자동 스폰한 플레이어 오브젝트를 올바른 위치로 배치하고,
    /// 사망 시 부활을 처리한다. 서버에서만 동작한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSpawnSystem : MonoBehaviour
    {
        public static PlayerSpawnSystem Instance { get; private set; }

        [Tooltip("비워 두면 자식 Transform 을 스폰 지점으로 쓴다.")]
        [SerializeField] private Transform[] spawnPoints;
        [Tooltip("스폰 지점이 겹치지 않도록 벌리는 반경.")]
        [SerializeField] private float spreadRadius = 1.5f;

        [Header("부활")]
        [SerializeField] private bool autoRespawn = true;
        [SerializeField] private float respawnDelay = 6f;
        [Range(0.1f, 1f)] [SerializeField] private float respawnHealthPercent = 0.6f;

        /// <summary>서버가 기억하는 최근 체크포인트. 던전 진행에 따라 갱신된다.</summary>
        public Transform ActiveCheckpoint { get; set; }

        private readonly Dictionary<ulong, Coroutine> _respawning = new Dictionary<ulong, Coroutine>();
        private int _spawnCursor;

        private void Awake()
        {
            Instance = this;
            if (spawnPoints == null || spawnPoints.Length == 0) CollectChildSpawnPoints();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unhook();
        }

        private void CollectChildSpawnPoints()
        {
            var list = new List<Transform>();
            foreach (Transform child in transform) list.Add(child);
            spawnPoints = list.ToArray();
        }

        private void Start()
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogWarning("[PlayerSpawnSystem] NetworkManager 가 없습니다. 싱글 플레이 씬이면 무시해도 됩니다.");
                return;
            }
            Hook();

            // 이미 접속해 있던 플레이어(호스트 포함)를 배치한다.
            if (NetworkManager.Singleton.IsServer) StartCoroutine(PlaceAllNextFrame());
        }

        private void Hook()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            nm.OnClientConnectedCallback += OnClientConnected;
            if (nm.SceneManager != null) nm.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
        }

        private void Unhook()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            nm.OnClientConnectedCallback -= OnClientConnected;
            if (nm.SceneManager != null) nm.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
        }

        private void OnSceneLoadCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            StartCoroutine(PlaceAllNextFrame());
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            StartCoroutine(PlaceNextFrame(clientId));
        }

        private IEnumerator PlaceAllNextFrame()
        {
            yield return null;
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds) Place(clientId);
        }

        private IEnumerator PlaceNextFrame(ulong clientId)
        {
            // 플레이어 오브젝트가 실제로 스폰될 때까지 한 프레임 기다린다.
            yield return null;
            Place(clientId);
        }

        private void Place(ulong clientId)
        {
            var playerObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObject == null) return;

            var identity = playerObject.GetComponent<PlayerNetworkIdentity>();
            if (identity == null) return;

            identity.ApplySessionData();
            GetSpawnPose(out var position, out var rotation);
            identity.Teleport(position, rotation);
        }

        /// <summary>다음 스폰 위치를 순환하며 고른다.</summary>
        public void GetSpawnPose(out Vector3 position, out Quaternion rotation)
        {
            Transform point = ActiveCheckpoint;

            if (point == null && spawnPoints != null && spawnPoints.Length > 0)
            {
                point = spawnPoints[_spawnCursor % spawnPoints.Length];
                _spawnCursor++;
            }

            if (point == null)
            {
                position = transform.position;
                rotation = transform.rotation;
                return;
            }

            Vector2 offset = Random.insideUnitCircle * spreadRadius;
            position = point.position + new Vector3(offset.x, 0f, offset.y);
            rotation = point.rotation;
        }

        // ---------------------------------------------------------------- 부활

        /// <summary>서버 전용. Health 가 죽었을 때 던전 쪽에서 호출한다.</summary>
        public void HandlePlayerDeath(PlayerNetworkIdentity identity)
        {
            if (!autoRespawn || identity == null) return;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (_respawning.ContainsKey(identity.OwnerClientId)) return;

            _respawning[identity.OwnerClientId] = StartCoroutine(RespawnRoutine(identity));
        }

        private IEnumerator RespawnRoutine(PlayerNetworkIdentity identity)
        {
            yield return new WaitForSeconds(respawnDelay);
            _respawning.Remove(identity.OwnerClientId);

            if (identity == null || !identity.IsSpawned) yield break;

            GetSpawnPose(out var position, out var rotation);
            identity.Teleport(position, rotation);

            var health = identity.GetComponent<Health>();
            if (health != null) health.Revive(respawnHealthPercent);
        }

        private void OnDrawGizmos()
        {
            if (spawnPoints == null) return;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.7f);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] == null) continue;
                Gizmos.DrawWireSphere(spawnPoints[i].position, spreadRadius);
                Gizmos.DrawRay(spawnPoints[i].position, spawnPoints[i].forward * 2f);
            }
        }
    }
}
