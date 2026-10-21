using System;
using System.Text;
using ActionRPG.Core;
using ActionRPG.Skills;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ActionRPG.Networking
{
    public enum ConnectionRole : byte
    {
        None = 0,
        Host = 1,
        Client = 2,
        DedicatedServer = 3
    }

    /// <summary>
    /// 호스트/클라이언트 시작과 접속 승인을 한곳에서 처리한다.
    /// 씬이 바뀌어도 살아 있어야 하므로 DontDestroyOnLoad 로 유지한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ConnectionManager : MonoBehaviour
    {
        public static ConnectionManager Instance { get; private set; }

        [SerializeField] private NetworkManager networkManager;
        [Tooltip("접속 승인 시 버전이 다르면 거절한다. 빌드가 섞이는 사고를 막는다.")]
        [SerializeField] private string buildVersion = "0.1.0";
        [Tooltip("호스트가 게임을 시작할 때 이동할 씬.")]
        [SerializeField] private string gameplaySceneName = GameConstants.Scenes.Dungeon01;

        public ConnectionRole Role { get; private set; } = ConnectionRole.None;
        public bool IsConnected => networkManager != null && (networkManager.IsClient || networkManager.IsServer);

        /// <summary>거절 사유. UI 가 보여 준다.</summary>
        public event Action<string> ConnectionFailed;
        public event Action ConnectedAsClient;
        public event Action Disconnected;
        public event Action<ulong> ClientConnected;
        public event Action<ulong> ClientDisconnected;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (networkManager == null) networkManager = GetComponent<NetworkManager>();
            if (networkManager == null) networkManager = NetworkManager.Singleton;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unhook();
        }

        private void Hook()
        {
            if (networkManager == null) return;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void Unhook()
        {
            if (networkManager == null) return;
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.ConnectionApprovalCallback -= ApproveConnection;
        }

        // ---------------------------------------------------------------- 시작

        public bool StartHost(ushort port = GameConstants.DefaultPort)
        {
            if (networkManager == null) { Fail("NetworkManager 를 찾을 수 없습니다."); return false; }

            ConfigureTransport("0.0.0.0", port, listen: true);
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = ApproveConnection;
            networkManager.NetworkConfig.ConnectionData = BuildPayload();

            Hook();
            if (!networkManager.StartHost())
            {
                Fail("호스트를 시작하지 못했습니다. 포트가 이미 사용 중일 수 있습니다.");
                return false;
            }

            Role = ConnectionRole.Host;
            return true;
        }

        public bool StartClient(string address = GameConstants.DefaultAddress, ushort port = GameConstants.DefaultPort)
        {
            if (networkManager == null) { Fail("NetworkManager 를 찾을 수 없습니다."); return false; }

            ConfigureTransport(address, port, listen: false);
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.NetworkConfig.ConnectionData = BuildPayload();

            Hook();
            if (!networkManager.StartClient())
            {
                Fail("접속을 시작하지 못했습니다.");
                return false;
            }

            Role = ConnectionRole.Client;
            return true;
        }

        public bool StartDedicatedServer(ushort port = GameConstants.DefaultPort)
        {
            if (networkManager == null) { Fail("NetworkManager 를 찾을 수 없습니다."); return false; }

            ConfigureTransport("0.0.0.0", port, listen: true);
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = ApproveConnection;

            Hook();
            if (!networkManager.StartServer())
            {
                Fail("서버를 시작하지 못했습니다.");
                return false;
            }

            Role = ConnectionRole.DedicatedServer;
            return true;
        }

        public void Shutdown(bool returnToMenu = true)
        {
            if (networkManager != null && (networkManager.IsClient || networkManager.IsServer))
                networkManager.Shutdown();

            SessionManager.Clear();
            Role = ConnectionRole.None;
            Unhook();
            Disconnected?.Invoke();

            if (returnToMenu && SceneManager.GetActiveScene().name != GameConstants.Scenes.MainMenu)
                SceneManager.LoadScene(GameConstants.Scenes.MainMenu);
        }

        /// <summary>호스트 전용. 네트워크 씬 매니저로 전원을 게임 씬으로 옮긴다.</summary>
        public void LoadGameplayScene(string sceneName = null)
        {
            if (networkManager == null || !networkManager.IsServer) return;
            string target = string.IsNullOrEmpty(sceneName) ? gameplaySceneName : sceneName;
            networkManager.SceneManager.LoadScene(target, LoadSceneMode.Single);
        }

        // ---------------------------------------------------------------- 승인

        private byte[] BuildPayload()
        {
            var payload = ConnectionPayload.FromLoadout(LoadoutStorage.PlayerName, LoadoutStorage.Current, buildVersion);
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        }

        /// <summary>서버에서만 호출된다. 여기서 정원·버전·덱 유효성을 검사한다.</summary>
        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = true;
            response.Pending = false;

            if (networkManager.ConnectedClientsIds.Count >= GameConstants.MaxPlayers)
            {
                response.Approved = false;
                response.Reason = $"방이 가득 찼습니다 (최대 {GameConstants.MaxPlayers}명).";
                return;
            }

            ConnectionPayload payload = null;
            if (request.Payload != null && request.Payload.Length > 0)
            {
                try
                {
                    payload = JsonUtility.FromJson<ConnectionPayload>(Encoding.UTF8.GetString(request.Payload));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ConnectionManager] 접속 페이로드 해석 실패: {e.Message}");
                }
            }

            if (payload == null)
            {
                response.Approved = false;
                response.Reason = "접속 정보가 올바르지 않습니다.";
                return;
            }

            if (!string.IsNullOrEmpty(buildVersion) && payload.buildVersion != buildVersion)
            {
                response.Approved = false;
                response.Reason = $"빌드 버전이 다릅니다 (서버 {buildVersion} / 클라이언트 {payload.buildVersion}).";
                return;
            }

            var loadout = payload.ToLoadout();
            var database = SkillDatabase.Instance;
            if (database != null && !database.Validate(loadout, out string reason))
            {
                Debug.LogWarning($"[ConnectionManager] {payload.playerName} 의 덱이 유효하지 않아 기본 덱으로 대체합니다: {reason}");
                loadout = default;
            }

            SessionManager.Register(request.ClientNetworkId, payload, payload.playerName + "@" + payload.buildVersion);
            var session = SessionManager.Get(request.ClientNetworkId);
            if (session != null) session.Loadout = loadout;

            response.Approved = true;
            // 스폰 위치는 PlayerSpawnSystem 이 씬 로드 후에 다시 잡는다.
        }

        private void ConfigureTransport(string address, ushort port, bool listen)
        {
            var transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[ConnectionManager] NetworkManager 에 UnityTransport 가 없습니다.");
                return;
            }

            transport.SetConnectionData(address, port, listen ? "0.0.0.0" : null);
        }

        private void OnClientConnected(ulong clientId)
        {
            ClientConnected?.Invoke(clientId);
            if (networkManager.IsClient && clientId == networkManager.LocalClientId) ConnectedAsClient?.Invoke();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (networkManager.IsServer) SessionManager.Unregister(clientId);
            ClientDisconnected?.Invoke(clientId);

            // 내가 끊긴 경우 (서버가 거절했을 수도 있다)
            if (clientId == networkManager.LocalClientId && !networkManager.IsServer)
            {
                string reason = networkManager.DisconnectReason;
                Fail(string.IsNullOrEmpty(reason) ? "서버와의 연결이 끊어졌습니다." : reason);
                Role = ConnectionRole.None;
                Disconnected?.Invoke();
            }
        }

        private void Fail(string reason)
        {
            Debug.LogWarning($"[ConnectionManager] {reason}");
            ConnectionFailed?.Invoke(reason);
        }
    }
}
