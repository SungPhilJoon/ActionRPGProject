using ActionRPG.Core;
using ActionRPG.InputSystem;
using ActionRPG.Networking;
using ActionRPG.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ActionRPG.UI
{
    /// <summary>
    /// 메인 메뉴. 튜토리얼 시작, 방 만들기, 참가, 덱 편집을 제공한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuView : MonoBehaviour
    {
        [Header("입력 필드")]
        [SerializeField] private TMP_InputField nameField;
        [SerializeField] private TMP_InputField addressField;
        [SerializeField] private TMP_InputField portField;

        [Header("버튼")]
        [SerializeField] private Button tutorialButton;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button deckButton;
        [SerializeField] private Button quitButton;

        [Header("패널")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject deckPanel;
        [SerializeField] private GameObject connectingPanel;
        [SerializeField] private TMP_Text statusText;

        [Header("씬")]
        [SerializeField] private string tutorialScene = GameConstants.Scenes.Tutorial;
        [SerializeField] private string dungeonScene = GameConstants.Scenes.Dungeon01;

        private void Awake()
        {
            if (tutorialButton != null) tutorialButton.onClick.AddListener(StartTutorial);
            if (hostButton != null) hostButton.onClick.AddListener(HostGame);
            if (joinButton != null) joinButton.onClick.AddListener(JoinGame);
            if (deckButton != null) deckButton.onClick.AddListener(OpenDeckBuilder);
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        }

        private void Start()
        {
            InputReader.SetCursorLocked(false);

            if (nameField != null)
            {
                nameField.text = LoadoutStorage.PlayerName;
                nameField.onEndEdit.AddListener(value => LoadoutStorage.PlayerName = value);
            }
            if (addressField != null && string.IsNullOrEmpty(addressField.text)) addressField.text = GameConstants.DefaultAddress;
            if (portField != null && string.IsNullOrEmpty(portField.text)) portField.text = GameConstants.DefaultPort.ToString();

            ShowMain();
        }

        private void OnEnable()
        {
            if (ConnectionManager.Instance != null)
            {
                ConnectionManager.Instance.ConnectionFailed += OnConnectionFailed;
                ConnectionManager.Instance.ConnectedAsClient += OnConnectedAsClient;
            }
        }

        private void OnDisable()
        {
            if (ConnectionManager.Instance != null)
            {
                ConnectionManager.Instance.ConnectionFailed -= OnConnectionFailed;
                ConnectionManager.Instance.ConnectedAsClient -= OnConnectedAsClient;
            }
        }

        // ---------------------------------------------------------------- 동작

        public void StartTutorial()
        {
            CommitName();
            SceneManager.LoadScene(tutorialScene);
        }

        public void HostGame()
        {
            CommitName();
            var connection = ConnectionManager.Instance;
            if (connection == null) { SetStatus("ConnectionManager 가 씬에 없습니다."); return; }

            if (!connection.StartHost(ReadPort())) return;

            SetStatus("방을 여는 중...");
            connection.LoadGameplayScene(dungeonScene);
        }

        public void JoinGame()
        {
            CommitName();
            var connection = ConnectionManager.Instance;
            if (connection == null) { SetStatus("ConnectionManager 가 씬에 없습니다."); return; }

            string address = addressField != null && !string.IsNullOrWhiteSpace(addressField.text)
                ? addressField.text.Trim()
                : GameConstants.DefaultAddress;

            if (!connection.StartClient(address, ReadPort())) return;

            ShowConnecting($"{address} 에 접속 중...");
        }

        public void OpenDeckBuilder()
        {
            if (deckPanel == null) return;
            if (mainPanel != null) mainPanel.SetActive(false);
            deckPanel.SetActive(true);
        }

        public void ShowMain()
        {
            if (mainPanel != null) mainPanel.SetActive(true);
            if (deckPanel != null) deckPanel.SetActive(false);
            if (connectingPanel != null) connectingPanel.SetActive(false);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------------------------------------------------------- 헬퍼

        private void CommitName()
        {
            if (nameField != null && !string.IsNullOrWhiteSpace(nameField.text))
                LoadoutStorage.PlayerName = nameField.text;
        }

        private ushort ReadPort()
        {
            if (portField == null) return GameConstants.DefaultPort;
            return ushort.TryParse(portField.text, out ushort port) && port > 0 ? port : GameConstants.DefaultPort;
        }

        private void ShowConnecting(string message)
        {
            if (mainPanel != null) mainPanel.SetActive(false);
            if (connectingPanel != null) connectingPanel.SetActive(true);
            SetStatus(message);
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
            else Debug.Log($"[MainMenu] {message}");
        }

        private void OnConnectionFailed(string reason)
        {
            ShowMain();
            SetStatus(reason);
        }

        private void OnConnectedAsClient()
        {
            SetStatus("접속 완료. 호스트를 기다리는 중...");
        }
    }
}
