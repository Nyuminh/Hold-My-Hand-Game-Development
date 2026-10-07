using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Threading;
using System.Threading.Tasks;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Quản lý Lobby 3D kiểu PUBG:
    /// - Hiện nhân vật 3D idle trong scene
    /// - UI overlay nổi lên trên
    /// - Tạo/join phòng qua backend API
    /// - Khi đủ người + bấm PLAY → load game scene
    /// </summary>
    public class Lobby3DManager : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("UIDocument chứa Lobby3DScreen.uxml")]
        [SerializeField] private UIDocument lobbyUI;

        [Header("3D Preview")]
        [Tooltip("GameObject nhân vật Player 1 (mình) — luôn hiện")]
        [SerializeField] private GameObject player1Preview;

        [Tooltip("GameObject nhân vật Player 2 (bạn) — ẩn ban đầu, hiện khi có người join")]
        [SerializeField] private GameObject player2Preview;

        [Header("Scene")]
        [Tooltip("Tên scene game sẽ load khi bấm PLAY")]
        [SerializeField] private string gameSceneName = "[BB] Core MultiplayerSession";

        [Header("Polling")]
        [Tooltip("Khoảng cách giữa các lần poll lobby state (giây)")]
        [SerializeField] private float pollInterval = 3f;

        // ─── UI Elements ────────────────────────────────────────
        // Top
        private Label m_PlayerNameLabel;
        private Label m_PlayerRoleLabel;
        private VisualElement m_RoomInfoPanel;
        private Label m_RoomCodeLabel;
        private Label m_RoomStatusLabel;
        private Button m_CopyCodeButton;

        // Bottom right — main
        private VisualElement m_MainButtonsPanel;
        private Button m_CreateRoomButton;
        private Button m_JoinRoomButton;
        private Button m_BrowseSessionsButton;

        // Bottom right — join code
        private VisualElement m_JoinCodePanel;
        private TextField m_JoinCodeInput;
        private Button m_ConfirmJoinButton;
        private Button m_CancelJoinButton;

        // Bottom right — in-lobby
        private VisualElement m_LobbyButtonsPanel;
        private Button m_PlayButton;
        private Button m_SwapRoleButton;
        private Button m_LeaveRoomButton;

        // Bottom left — session list
        private VisualElement m_SessionListPanel;
        private VisualElement m_BottomLeftSpacer;
        private ScrollView m_SessionScroll;
        private Label m_NoSessionsLabel;
        private Button m_RefreshSessionsButton;
        private Button m_CloseSessionsButton;

        // Center toast
        private Label m_ToastLabel;

        // ─── State ──────────────────────────────────────────────
        private string m_CurrentLobbyCode;
        private bool m_IsHost;
        private CancellationTokenSource m_PollCts;

        // ────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void OnEnable()
        {
            if (lobbyUI == null)
            {
                Debug.LogError("[Lobby3DManager] lobbyUI is not assigned.", this);
                return;
            }

            SetupUI();
            SetupPreview();

            // Show player info
            UpdatePlayerInfo();
        }

        private void Start()
        {
            // RẤT QUAN TRỌNG: Đảm bảo chuột hiện lên để có thể click UI
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private void OnDisable()
        {
            StopPolling();
        }

        private void OnDestroy()
        {
            StopPolling();
        }

        #endregion

        // ────────────────────────────────────────────────────────
        #region UI Setup

        private void SetupUI()
        {
            var root = lobbyUI.rootVisualElement;

            // QUAN TRỌNG: UIDocument tự tạo 1 lớp root vô hình phủ full màn hình.
            // Phải set Ignore để click xuyên qua vùng trống, chỉ các child có Position mới nhận click.
            root.pickingMode = PickingMode.Ignore;

            // Top
            m_PlayerNameLabel = root.Q<Label>("player-name-label");
            m_PlayerRoleLabel = root.Q<Label>("player-role-label");
            m_RoomInfoPanel = root.Q<VisualElement>("room-info-panel");
            m_RoomCodeLabel = root.Q<Label>("room-code-label");
            m_RoomStatusLabel = root.Q<Label>("room-status-label");
            m_CopyCodeButton = root.Q<Button>("copy-code-button");

            // Main buttons
            m_MainButtonsPanel = root.Q<VisualElement>("main-buttons-panel");
            m_CreateRoomButton = root.Q<Button>("create-room-button");
            m_JoinRoomButton = root.Q<Button>("join-room-button");
            m_BrowseSessionsButton = root.Q<Button>("browse-sessions-button");

            // Join code panel
            m_JoinCodePanel = root.Q<VisualElement>("join-code-panel");
            m_JoinCodeInput = root.Q<TextField>("join-code-input");
            m_ConfirmJoinButton = root.Q<Button>("confirm-join-button");
            m_CancelJoinButton = root.Q<Button>("cancel-join-button");

            // Lobby buttons
            m_LobbyButtonsPanel = root.Q<VisualElement>("lobby-buttons-panel");
            m_PlayButton = root.Q<Button>("play-button");
            m_SwapRoleButton = root.Q<Button>("swap-role-button");
            m_LeaveRoomButton = root.Q<Button>("leave-room-button");

            // Session list
            m_SessionListPanel = root.Q<VisualElement>("session-list-panel");
            m_BottomLeftSpacer = root.Q<VisualElement>("bottom-left-spacer");
            m_SessionScroll = root.Q<ScrollView>("session-scroll");
            m_NoSessionsLabel = root.Q<Label>("no-sessions-label");
            m_RefreshSessionsButton = root.Q<Button>("refresh-sessions-button");
            m_CloseSessionsButton = root.Q<Button>("close-sessions-button");

            // Toast
            m_ToastLabel = root.Q<Label>("toast-label");
            if (m_ToastLabel != null) m_ToastLabel.text = "";

            // Event bindings
            if (m_CreateRoomButton != null) m_CreateRoomButton.clicked += OnCreateRoomClicked;
            if (m_JoinRoomButton != null) m_JoinRoomButton.clicked += OnJoinRoomClicked;
            if (m_BrowseSessionsButton != null) m_BrowseSessionsButton.clicked += OnBrowseSessionsClicked;
            if (m_ConfirmJoinButton != null) m_ConfirmJoinButton.clicked += OnConfirmJoinClicked;
            if (m_CancelJoinButton != null) m_CancelJoinButton.clicked += OnCancelJoinClicked;
            if (m_CopyCodeButton != null) m_CopyCodeButton.clicked += OnCopyCodeClicked;
            if (m_PlayButton != null) m_PlayButton.clicked += OnPlayClicked;
            if (m_SwapRoleButton != null) m_SwapRoleButton.clicked += OnSwapRoleClicked;
            if (m_LeaveRoomButton != null) m_LeaveRoomButton.clicked += OnLeaveRoomClicked;
            if (m_RefreshSessionsButton != null) m_RefreshSessionsButton.clicked += OnRefreshSessionsClicked;
            if (m_CloseSessionsButton != null) m_CloseSessionsButton.clicked += OnCloseSessionsClicked;

            // Initial state: show main buttons, hide lobby buttons
            ShowMainButtons();
        }

        private void SetupPreview()
        {
            // Player 1 luôn hiện (nhân vật mình)
            if (player1Preview != null)
                player1Preview.SetActive(true);

            // Player 2 ẩn ban đầu
            if (player2Preview != null)
                player2Preview.SetActive(false);
        }

        private void UpdatePlayerInfo()
        {
            if (m_PlayerNameLabel != null)
            {
                string name = ApiClient.IsLoggedIn
                    ? (!string.IsNullOrEmpty(ApiClient.DisplayName) ? ApiClient.DisplayName : ApiClient.Username)
                    : "Player";
                m_PlayerNameLabel.text = $"Xin chào, {name}!";
            }

            if (m_PlayerRoleLabel != null)
                m_PlayerRoleLabel.text = "Vai trò: ---";
        }

        #endregion

        // ────────────────────────────────────────────────────────
        #region UI State Switching

        private void ShowMainButtons()
        {
            if (m_MainButtonsPanel != null) m_MainButtonsPanel.style.display = DisplayStyle.Flex;
            if (m_LobbyButtonsPanel != null) m_LobbyButtonsPanel.style.display = DisplayStyle.None;
            if (m_JoinCodePanel != null) m_JoinCodePanel.style.display = DisplayStyle.None;
            if (m_RoomInfoPanel != null) m_RoomInfoPanel.style.display = DisplayStyle.None;
        }

        private void ShowLobbyButtons()
        {
            if (m_MainButtonsPanel != null) m_MainButtonsPanel.style.display = DisplayStyle.None;
            if (m_LobbyButtonsPanel != null) m_LobbyButtonsPanel.style.display = DisplayStyle.Flex;
            if (m_JoinCodePanel != null) m_JoinCodePanel.style.display = DisplayStyle.None;
            if (m_RoomInfoPanel != null) m_RoomInfoPanel.style.display = DisplayStyle.Flex;
        }

        private void ShowJoinCodePanel()
        {
            if (m_JoinCodePanel != null) m_JoinCodePanel.style.display = DisplayStyle.Flex;
        }

        private void HideJoinCodePanel()
        {
            if (m_JoinCodePanel != null) m_JoinCodePanel.style.display = DisplayStyle.None;
        }

        #endregion

        // ────────────────────────────────────────────────────────
        #region Button Handlers

        private async void OnCreateRoomClicked()
        {
            if (m_CreateRoomButton != null) m_CreateRoomButton.SetEnabled(false);
            ShowToast("Đang tạo phòng...");

            try
            {
                var lobby = await ApiClient.CreateLobbyAsync();
                m_CurrentLobbyCode = lobby.lobbyCode;
                m_IsHost = true;

                Debug.Log($"[Lobby3D] Created lobby: {lobby.lobbyCode}");
                UpdateLobbyUI(lobby);
                ShowLobbyButtons();
                StartPolling();
                ShowToast("Phòng đã được tạo!");
            }
            catch (ApiException apiEx)
            {
                if (apiEx.StatusCode == 401)
                {
                    ShowToast("Phiên đăng nhập hết hạn! Vui lòng quay lại LoginScene để đăng nhập lại.");
                }
                else if (apiEx.StatusCode == 400 && apiEx.ResponseBody != null && apiEx.ResponseBody.Contains("already in a lobby"))
                {
                    ShowToast("Bạn đang bị kẹt ở phòng cũ. Hãy đợi 30 phút hoặc reset backend.");
                }
                else
                {
                    ShowToast($"Tạo phòng thất bại: {apiEx.Message}");
                }
                Debug.LogError($"[Lobby3D] Create lobby error: {apiEx}");
                if (m_CreateRoomButton != null) m_CreateRoomButton.SetEnabled(true);
            }
            catch (System.Exception ex)
            {
                ShowToast("Không thể kết nối server!");
                Debug.LogError($"[Lobby3D] Create lobby error: {ex}");
                if (m_CreateRoomButton != null) m_CreateRoomButton.SetEnabled(true);
            }
        }

        private void OnJoinRoomClicked()
        {
            ShowJoinCodePanel();
            if (m_JoinCodeInput != null) m_JoinCodeInput.value = "";
        }

        private void OnCancelJoinClicked()
        {
            HideJoinCodePanel();
        }

        private async void OnConfirmJoinClicked()
        {
            string code = m_JoinCodeInput?.value?.Trim().ToUpper();
            if (string.IsNullOrWhiteSpace(code))
            {
                ShowToast("Nhập mã phòng!");
                return;
            }

            if (m_ConfirmJoinButton != null) m_ConfirmJoinButton.SetEnabled(false);
            ShowToast("Đang tham gia...");

            try
            {
                // Nếu đã có lobby, rời trước
                if (!string.IsNullOrEmpty(m_CurrentLobbyCode))
                {
                    StopPolling();
                    try { await ApiClient.LeaveLobbyAsync(m_CurrentLobbyCode); } catch { }
                }

                var lobby = await ApiClient.JoinLobbyAsync(code);
                m_CurrentLobbyCode = lobby.lobbyCode;
                m_IsHost = false;

                Debug.Log($"[Lobby3D] Joined lobby: {lobby.lobbyCode}");
                HideJoinCodePanel();
                UpdateLobbyUI(lobby);
                ShowLobbyButtons();
                StartPolling();
                ShowToast("Đã vào phòng!");
            }
            catch (ApiException apiEx)
            {
                string errorMsg = apiEx.StatusCode switch
                {
                    404 => "Phòng không tồn tại!",
                    400 => "Phòng đã đầy hoặc không khả dụng!",
                    _ => $"Lỗi: {apiEx.Message}"
                };
                ShowToast(errorMsg);
                if (m_ConfirmJoinButton != null) m_ConfirmJoinButton.SetEnabled(true);
            }
            catch (System.Exception ex)
            {
                ShowToast("Không thể kết nối!");
                Debug.LogError($"[Lobby3D] Join error: {ex}");
                if (m_ConfirmJoinButton != null) m_ConfirmJoinButton.SetEnabled(true);
            }
        }

        private void OnCopyCodeClicked()
        {
            if (!string.IsNullOrEmpty(m_CurrentLobbyCode))
            {
                GUIUtility.systemCopyBuffer = m_CurrentLobbyCode;
                ShowToast("Đã copy mã phòng!");
            }
        }

        private async void OnSwapRoleClicked()
        {
            if (string.IsNullOrEmpty(m_CurrentLobbyCode)) return;
            if (m_SwapRoleButton != null) m_SwapRoleButton.SetEnabled(false);

            try
            {
                var lobby = await ApiClient.SwapRolesAsync(m_CurrentLobbyCode);
                UpdateLobbyUI(lobby);
                ShowToast("Đã đổi vai!");
            }
            catch (System.Exception ex)
            {
                ShowToast("Đổi vai thất bại!");
                Debug.LogError($"[Lobby3D] Swap error: {ex}");
            }
            finally
            {
                if (m_SwapRoleButton != null) m_SwapRoleButton.SetEnabled(true);
            }
        }

        private async void OnLeaveRoomClicked()
        {
            if (string.IsNullOrEmpty(m_CurrentLobbyCode)) return;
            StopPolling();
            if (m_LeaveRoomButton != null) m_LeaveRoomButton.SetEnabled(false);

            try
            {
                await ApiClient.LeaveLobbyAsync(m_CurrentLobbyCode);
                Debug.Log($"[Lobby3D] Left lobby: {m_CurrentLobbyCode}");
                m_CurrentLobbyCode = null;

                // Ẩn player 2 preview
                if (player2Preview != null) player2Preview.SetActive(false);

                ShowMainButtons();
                if (m_CreateRoomButton != null) m_CreateRoomButton.SetEnabled(true);
                ShowToast("Đã rời phòng.");
            }
            catch (System.Exception ex)
            {
                ShowToast("Rời phòng thất bại!");
                Debug.LogError($"[Lobby3D] Leave error: {ex}");
                if (m_LeaveRoomButton != null) m_LeaveRoomButton.SetEnabled(true);
            }
        }

        private async void OnPlayClicked()
        {
            if (string.IsNullOrEmpty(m_CurrentLobbyCode)) return;
            if (m_PlayButton != null) m_PlayButton.SetEnabled(false);
            if (m_SwapRoleButton != null) m_SwapRoleButton.SetEnabled(false);
            if (m_LeaveRoomButton != null) m_LeaveRoomButton.SetEnabled(false);
            ShowToast("Đang khởi động game...");

            try
            {
                var result = await ApiClient.ReadyAsync(m_CurrentLobbyCode);
                StopPolling();

                Debug.Log($"[Lobby3D] Game started! SessionId={result.sessionId}, Relay={result.relayJoinCode}");
                ShowToast("🎮 Game bắt đầu!");

                // Load game scene
                SceneManager.LoadScene(gameSceneName);
            }
            catch (System.Exception ex)
            {
                ShowToast("Bắt đầu game thất bại!");
                Debug.LogError($"[Lobby3D] Play error: {ex}");
                if (m_PlayButton != null) m_PlayButton.SetEnabled(true);
                if (m_SwapRoleButton != null) m_SwapRoleButton.SetEnabled(true);
                if (m_LeaveRoomButton != null) m_LeaveRoomButton.SetEnabled(true);
            }
        }

        // ─── Session Browser ────────────────────────────────────
        private void OnBrowseSessionsClicked()
        {
            if (m_SessionListPanel != null) m_SessionListPanel.style.display = DisplayStyle.Flex;
            if (m_BottomLeftSpacer != null) m_BottomLeftSpacer.style.display = DisplayStyle.None;
            OnRefreshSessionsClicked();
        }

        private void OnCloseSessionsClicked()
        {
            if (m_SessionListPanel != null) m_SessionListPanel.style.display = DisplayStyle.None;
            if (m_BottomLeftSpacer != null) m_BottomLeftSpacer.style.display = DisplayStyle.Flex;
        }

        private async void OnRefreshSessionsClicked()
        {
            if (m_RefreshSessionsButton != null) m_RefreshSessionsButton.SetEnabled(false);
            ShowToast("Đang tải danh sách phòng...");

            try
            {
                // Gọi API lấy danh sách lobby (nếu backend hỗ trợ)
                // Hiện tại backend chưa có endpoint list lobbies, nên hiện thông báo
                if (m_NoSessionsLabel != null)
                    m_NoSessionsLabel.text = "Tính năng tìm phòng đang được phát triển.\nHãy dùng mã phòng để join!";
                ShowToast("");
            }
            catch (System.Exception ex)
            {
                ShowToast("Không thể tải danh sách!");
                Debug.LogError($"[Lobby3D] Refresh sessions error: {ex}");
            }
            finally
            {
                if (m_RefreshSessionsButton != null) m_RefreshSessionsButton.SetEnabled(true);
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────
        #region Polling

        private void StartPolling()
        {
            StopPolling();
            m_PollCts = new CancellationTokenSource();
            _ = PollLobbyLoop(m_PollCts.Token);
        }

        private void StopPolling()
        {
            if (m_PollCts != null)
            {
                m_PollCts.Cancel();
                m_PollCts.Dispose();
                m_PollCts = null;
            }
        }

        private async Task PollLobbyLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && !string.IsNullOrEmpty(m_CurrentLobbyCode))
            {
                try { await Task.Delay((int)(pollInterval * 1000), ct); }
                catch (TaskCanceledException) { break; }
                if (ct.IsCancellationRequested) break;

                try
                {
                    var lobby = await ApiClient.GetLobbyAsync(m_CurrentLobbyCode);
                    UpdateLobbyUI(lobby);

                    // Nếu host đã bấm PLAY (status = InGame), joiner cũng auto load game
                    if (lobby.status == "InGame" && !m_IsHost)
                    {
                        StopPolling();
                        ShowToast("🎮 Host đã bắt đầu game!");
                        await Task.Delay(500); // Chờ chút cho người chơi thấy thông báo
                        SceneManager.LoadScene(gameSceneName);
                        break;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[Lobby3D] Poll: {ex.Message}");
                }
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────
        #region UI Helpers

        private void UpdateLobbyUI(ApiClient.LobbyResponseData lobby)
        {
            // Room code
            if (m_RoomCodeLabel != null)
                m_RoomCodeLabel.text = lobby.lobbyCode ?? "------";

            // Status
            bool hasPlayer2 = !string.IsNullOrEmpty(lobby.player2Id);
            if (m_RoomStatusLabel != null)
            {
                if (hasPlayer2)
                {
                    m_RoomStatusLabel.text = "✅ Sẵn sàng!";
                    m_RoomStatusLabel.style.color = new StyleColor(new Color(0.3f, 1f, 0.5f));
                }
                else
                {
                    m_RoomStatusLabel.text = "Đang chờ người chơi thứ 2... (1/2)";
                    m_RoomStatusLabel.style.color = new StyleColor(new Color(1f, 0.8f, 0.4f));
                }
            }

            // Role display
            if (m_PlayerRoleLabel != null)
            {
                string myId = ApiClient.UserId;
                if (!string.IsNullOrEmpty(myId))
                {
                    if (myId == lobby.dadUserId)
                        m_PlayerRoleLabel.text = "Vai trò: 🧔 Ba";
                    else if (myId == lobby.childUserId)
                        m_PlayerRoleLabel.text = "Vai trò: 👶 Con";
                    else
                        m_PlayerRoleLabel.text = "Vai trò: ---";
                }
            }

            // Player 2 preview — hiện/ẩn nhân vật thứ 2 trong 3D
            if (player2Preview != null)
                player2Preview.SetActive(hasPlayer2);

            // PLAY button — chỉ enable khi đủ 2 người + status Waiting
            bool canPlay = hasPlayer2 && lobby.status == "Waiting";
            if (m_PlayButton != null)
            {
                m_PlayButton.SetEnabled(canPlay);
                if (canPlay)
                {
                    m_PlayButton.style.backgroundColor = new StyleColor(new Color(0.94f, 0.63f, 0.16f)); // Vàng cam
                    m_PlayButton.style.color = new StyleColor(Color.white);
                }
                else
                {
                    m_PlayButton.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.1f));
                    m_PlayButton.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.3f));
                }
            }

            // Swap chỉ khi đủ 2 người
            if (m_SwapRoleButton != null)
                m_SwapRoleButton.SetEnabled(canPlay);
        }

        private void ShowToast(string message)
        {
            if (m_ToastLabel != null)
            {
                m_ToastLabel.text = message;
                m_ToastLabel.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        #endregion
    }
}
