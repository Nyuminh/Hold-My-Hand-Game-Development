using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using Cursor = UnityEngine.Cursor;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Quản lý menu Pause / Escape khi đang ở trong scene gameplay ([BB] Core MultiplayerSession):
    /// - Lắng nghe phím ESC để bật/tắt UI Tạm dừng, khóa/mở chuột.
    /// - Nút "Tiếp tục" đóng menu và tiếp tục chơi.
    /// - Nút "Trở về sảnh chờ" (Back to Lobby): Đồng bộ đưa CẢ 2 NGƯỜI CHƠI cùng về scene Lobby.
    /// - Bất kể Host hay Client bấm thoát, cả phòng đều được điều hướng về sảnh chờ đồng bộ.
    /// - Tự động fallback đưa client về Lobby nếu Host ngắt kết nối đột ngột.
    /// </summary>
    public class InGameEscapeMenuManager : MonoBehaviour
    {
        public static InGameEscapeMenuManager Instance { get; private set; }

        private const string MsgReturnToLobby = "HMH_ReturnToLobby";
        private const string MsgRequestReturnToLobby = "HMH_RequestReturnToLobby";

        [Header("UI Document")]
        [Tooltip("UIDocument chứa InGameEscapeMenu.uxml")]
        [SerializeField] private UIDocument escapeUI;

        [Tooltip("VisualTreeAsset InGameEscapeMenu.uxml (nếu cần tự sinh)")]
        [SerializeField] private VisualTreeAsset escapeMenuUxml;

        [Header("Scene Configuration")]
        [Tooltip("Tên scene Lobby sẽ load khi thoát")]
        [SerializeField] private string lobbySceneName = "Lobby";

        // UI Elements
        private VisualElement m_Root;
        private Label m_StatusLabel;
        private Button m_ResumeButton;
        private Button m_BackToLobbyButton;

        private bool m_IsOpen = false;
        private bool m_IsQuitting = false;
        private bool m_HandlersRegistered = false;

        private CoreMovement m_LocalMovement;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name.Contains("Core") && !scene.name.Contains("Lobby") && !scene.name.Contains("Login"))
            {
                EnsureInstanceInScene();
            }
        }

        public static InGameEscapeMenuManager EnsureInstanceInScene()
        {
            if (Instance != null) return Instance;

            var existing = FindAnyObjectByType<InGameEscapeMenuManager>();
            if (existing != null) return existing;

            var prefab = Resources.Load<GameObject>("Pfb_InGameEscapeMenu");
            if (prefab != null)
            {
                var go = Instantiate(prefab);
                go.name = "[InGameEscapeMenu]";
                return go.GetComponent<InGameEscapeMenuManager>();
            }

            return null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            SetupUI();
        }

        private void Start()
        {
            RegisterNetworkHandlers();
        }

        private void OnEnable()
        {
            RegisterNetworkHandlers();
        }

        private void OnDisable()
        {
            UnregisterNetworkHandlers();
        }

        private void OnDestroy()
        {
            UnregisterNetworkHandlers();
        }

        private void SetupUI()
        {
            if (escapeUI == null)
            {
                escapeUI = GetComponent<UIDocument>();
            }

            if (escapeUI == null)
            {
                escapeUI = gameObject.AddComponent<UIDocument>();
                if (escapeMenuUxml != null)
                {
                    escapeUI.visualTreeAsset = escapeMenuUxml;
                }
            }

            if (escapeUI == null || escapeUI.rootVisualElement == null) return;

            var root = escapeUI.rootVisualElement;
            m_Root = root.Q<VisualElement>("escape-menu-root");
            m_StatusLabel = root.Q<Label>("escape-status-label");
            m_ResumeButton = root.Q<Button>("resume-button");
            m_BackToLobbyButton = root.Q<Button>("back-to-lobby-button");

            if (m_ResumeButton != null) m_ResumeButton.clicked += OnResumeClicked;
            if (m_BackToLobbyButton != null) m_BackToLobbyButton.clicked += OnBackToLobbyClicked;

            // Bind âm thanh Hover & Click
            UISoundManager.BindButtons(root);

            // Ban đầu ẩn menu
            if (m_Root != null)
            {
                m_Root.style.display = DisplayStyle.None;
            }
            m_IsOpen = false;
        }

        private void Update()
        {
            if (m_IsQuitting) return;

            bool escPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                escPressed = true;
            }
#endif
            if (!escPressed && Input.GetKeyDown(KeyCode.Escape))
            {
                escPressed = true;
            }

            if (escPressed)
            {
                ToggleMenu();
            }
        }

        public void ToggleMenu()
        {
            if (m_IsOpen)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }
        }

        public void OpenMenu()
        {
            if (m_Root == null) return;
            m_IsOpen = true;
            m_Root.style.display = DisplayStyle.Flex;

            // Mở khóa chuột
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Khóa di chuyển tạm thời
            SetPlayerMovementLocked(true);

            UISoundManager.PlayClick();
        }

        public void CloseMenu()
        {
            if (m_Root == null) return;
            m_IsOpen = false;
            m_Root.style.display = DisplayStyle.None;

            // Khóa lại chuột nếu không ở trong phòng chờ
            var waitRoom = FindAnyObjectByType<WaitRoomManager>();
            bool isInWaitRoom = waitRoom != null && (waitRoom.GetComponent<UIDocument>()?.rootVisualElement?.style.display == DisplayStyle.Flex);

            if (!isInWaitRoom)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                SetPlayerMovementLocked(false);
            }

            UISoundManager.PlayCancel();
        }

        private void SetPlayerMovementLocked(bool locked)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
            {
                var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
                if (playerObj != null)
                {
                    if (m_LocalMovement == null)
                        m_LocalMovement = playerObj.GetComponent<CoreMovement>();

                    if (m_LocalMovement != null)
                    {
                        m_LocalMovement.enabled = !locked;
                        if (locked) m_LocalMovement.ResetMovementForces();
                    }
                }
            }
        }

        private void OnResumeClicked()
        {
            CloseMenu();
        }

        private void OnBackToLobbyClicked()
        {
            if (m_IsQuitting) return;
            m_IsQuitting = true;

            UISoundManager.PlayConfirm();

            if (m_BackToLobbyButton != null) m_BackToLobbyButton.SetEnabled(false);
            if (m_ResumeButton != null) m_ResumeButton.SetEnabled(false);

            SetStatus("Đang đưa cả 2 người chơi về sảnh chờ...");

            StartCoroutine(ReturnToLobbyRoutine(isInitiator: true));
        }

        public void SetStatus(string text)
        {
            if (m_StatusLabel != null)
            {
                m_StatusLabel.text = text;
            }
        }

        #region Multiplayer 2-Player Synchronization

        private void RegisterNetworkHandlers()
        {
            if (m_HandlersRegistered || NetworkManager.Singleton == null) return;

            // Đăng ký disconnect callback
            NetworkManager.Singleton.OnClientDisconnectCallback += OnNetworkClientDisconnected;

            // Đăng ký custom messaging nếu có CustomMessagingManager
            var msgMgr = NetworkManager.Singleton.CustomMessagingManager;
            if (msgMgr != null)
            {
                msgMgr.RegisterNamedMessageHandler(MsgReturnToLobby, HandleReturnToLobbyMessage);
                msgMgr.RegisterNamedMessageHandler(MsgRequestReturnToLobby, HandleRequestReturnToLobbyMessage);
                m_HandlersRegistered = true;
                Debug.Log("[InGameEscapeMenu] Đã đăng ký Network Message Handlers thành công.");
            }
        }

        private void UnregisterNetworkHandlers()
        {
            if (!m_HandlersRegistered || NetworkManager.Singleton == null) return;

            NetworkManager.Singleton.OnClientDisconnectCallback -= OnNetworkClientDisconnected;

            var msgMgr = NetworkManager.Singleton.CustomMessagingManager;
            if (msgMgr != null)
            {
                msgMgr.UnregisterNamedMessageHandler(MsgReturnToLobby);
                msgMgr.UnregisterNamedMessageHandler(MsgRequestReturnToLobby);
            }

            m_HandlersRegistered = false;
        }

        /// <summary>
        /// Xử lý khi Host nhận được yêu cầu thoát phòng từ Client.
        /// </summary>
        private void HandleRequestReturnToLobbyMessage(ulong senderClientId, FastBufferReader reader)
        {
            Debug.Log($"[InGameEscapeMenu] Server nhận yêu cầu về Lobby từ Client {senderClientId}.");
            if (m_IsQuitting) return;
            m_IsQuitting = true;

            SetStatus("Người chơi cùng đã yêu cầu về sảnh. Đang chuyển cảnh...");
            StartCoroutine(ReturnToLobbyRoutine(isInitiator: true));
        }

        /// <summary>
        /// Xử lý khi Client nhận được broadcast yêu cầu về Lobby từ Host.
        /// </summary>
        private void HandleReturnToLobbyMessage(ulong senderClientId, FastBufferReader reader)
        {
            Debug.Log("[InGameEscapeMenu] Client nhận lệnh về Lobby từ Host.");
            if (m_IsQuitting) return;
            m_IsQuitting = true;

            SetStatus("Trưởng phòng đã đưa phòng về sảnh chờ...");
            StartCoroutine(ReturnToLobbyRoutine(isInitiator: false));
        }

        /// <summary>
        /// Fallback an toàn: nếu Host hoặc Client ngắt kết nối đột ngột, người còn lại sẽ tự động về Lobby.
        /// </summary>
        private void OnNetworkClientDisconnected(ulong clientId)
        {
            if (m_IsQuitting) return;

            // Nếu người bị ngắt là bản thân, hoặc người ngắt là Host (ServerClientId)
            if (clientId == NetworkManager.Singleton.LocalClientId || clientId == NetworkManager.ServerClientId)
            {
                Debug.LogWarning("[InGameEscapeMenu] Mất kết nối session. Đang tự động chuyển về Lobby...");
                m_IsQuitting = true;
                SetStatus("Phiên kết nối đã đóng. Đang trở về sảnh...");
                StartCoroutine(ReturnToLobbyRoutine(isInitiator: false));
            }
        }

        private IEnumerator ReturnToLobbyRoutine(bool isInitiator)
        {
            // Mở menu nếu chưa mở để hiện thông báo rõ ràng
            if (!m_IsOpen && m_Root != null)
            {
                m_Root.style.display = DisplayStyle.Flex;
                m_IsOpen = true;
            }

            // Mở chuột
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                var msgMgr = NetworkManager.Singleton.CustomMessagingManager;

                if (NetworkManager.Singleton.IsServer)
                {
                    // Host thông báo cho tất cả Client cùng về
                    if (isInitiator && msgMgr != null)
                    {
                        using (var writer = new FastBufferWriter(sizeof(byte), Unity.Collections.Allocator.Temp))
                        {
                            writer.WriteValueSafe((byte)1);
                            msgMgr.SendNamedMessageToAll(MsgReturnToLobby, writer);
                            Debug.Log("[InGameEscapeMenu] Host đã phát sóng lệnh ReturnToLobby cho tất cả client.");
                        }
                        // Chờ 300ms để đảm bảo gói tin mạng được gửi đi
                        yield return new WaitForSecondsRealtime(0.3f);
                    }
                }
                else
                {
                    // Client gửi yêu cầu lên Host
                    if (isInitiator && msgMgr != null)
                    {
                        using (var writer = new FastBufferWriter(sizeof(byte), Unity.Collections.Allocator.Temp))
                        {
                            writer.WriteValueSafe((byte)1);
                            msgMgr.SendNamedMessage(MsgRequestReturnToLobby, NetworkManager.ServerClientId, writer);
                            Debug.Log("[InGameEscapeMenu] Client đã gửi yêu cầu ReturnToLobby lên Server.");
                        }
                        // Chờ phản hồi hoặc timeout 1.5s
                        yield return new WaitForSecondsRealtime(1.0f);
                    }
                }
            }

            // Cleanup Network & Multiplayer Services
            CleanupNetworkSession();

            yield return new WaitForSecondsRealtime(0.2f);

            // Chuyển về Scene Lobby
            Debug.Log($"[InGameEscapeMenu] Đang load scene: {lobbySceneName}");
            SceneManager.LoadScene(lobbySceneName);
        }

        private async void CleanupNetworkSession()
        {
            try
            {
                if (MultiplayerService.Instance?.Sessions != null)
                {
                    var session = MultiplayerService.Instance.Sessions["default-session"];
                    if (session != null)
                    {
                        await session.LeaveAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InGameEscapeMenu] LeaveAsync warning: {ex.Message}");
            }

            try
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InGameEscapeMenu] NetworkManager Shutdown warning: {ex.Message}");
            }
        }

        #endregion
    }
}
