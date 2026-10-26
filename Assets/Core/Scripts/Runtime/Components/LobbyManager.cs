using UnityEngine;
using UnityEngine.UIElements;
using System.Threading;
using System.Threading.Tasks;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Quản lý Lobby theo flow PUBG-style:
    /// - Đăng nhập xong → vào lobby scene → tự động tạo lobby
    /// - Hiện lobby code để bạn bè join
    /// - Bạn bè nhập code để join
    /// - Bấm PLAY để bắt đầu game
    /// 
    /// Script này nên được gắn trong lobby scene, sẽ TỰ ĐỘNG tạo lobby khi scene load.
    /// </summary>
    public class LobbyManager : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("UIDocument chứa LobbyScreen.uxml")]
        [SerializeField] private UIDocument lobbyUI;

        [Header("Old UI to Hide")]
        [Tooltip("SessionBrowser UIDocument cũ — sẽ bị ẩn khi LobbyManager chạy")]
        [SerializeField] private UIDocument oldSessionBrowserUI;

        [Header("Polling")]
        [Tooltip("Khoảng cách giữa các lần poll lobby state (giây)")]
        [SerializeField] private float pollInterval = 3f;

        // UI elements — Main lobby overlay
        private VisualElement m_RootPanel;
        private Label m_WelcomeLabel;
        private Label m_LobbyCodeLabel;
        private Label m_StatusLabel;
        private Label m_DadLabel;
        private Label m_ChildLabel;
        private Button m_PlayButton;
        private Button m_LeaveButton;
        private Button m_SwapButton;
        private Button m_CopyCodeButton;

        // Join panel (for people who want to join existing lobby)
        private VisualElement m_JoinPanel;
        private TextField m_JoinCodeInput;
        private Button m_JoinButton;
        private Button m_ShowJoinButton;
        private Button m_BackToHostButton;

        // State
        private string m_CurrentLobbyCode;
        private CancellationTokenSource m_PollCts;
        private bool m_IsHost;

        private async void Start()
        {
            if (lobbyUI == null)
            {
                Debug.LogError("[LobbyManager] lobbyUI is not assigned.", this);
                return;
            }

            // Ẩn UI SessionBrowser cũ nếu có
            HideOldSessionBrowser();

            // Setup UI
            SetupUI();

            // Tự động tạo lobby khi scene load
            await AutoCreateLobby();
        }

        private void OnDestroy()
        {
            StopPolling();
        }

        // ─── Setup ──────────────────────────────────────────────────

        private void HideOldSessionBrowser()
        {
            if (oldSessionBrowserUI != null)
            {
                oldSessionBrowserUI.gameObject.SetActive(false);
                Debug.Log("[LobbyManager] Hidden old SessionBrowser UI.");
            }
        }

        private void SetupUI()
        {
            var root = lobbyUI.rootVisualElement;

            // Main lobby panel
            m_RootPanel = root.Q<VisualElement>("lobby-main-panel");
            m_WelcomeLabel = root.Q<Label>("welcome-label");
            m_LobbyCodeLabel = root.Q<Label>("lobby-code-label");
            m_StatusLabel = root.Q<Label>("lobby-status-label");
            m_DadLabel = root.Q<Label>("dad-label");
            m_ChildLabel = root.Q<Label>("child-label");
            m_PlayButton = root.Q<Button>("play-button");
            m_LeaveButton = root.Q<Button>("leave-button");
            m_SwapButton = root.Q<Button>("swap-button");
            m_CopyCodeButton = root.Q<Button>("copy-code-button");

            // Join panel
            m_JoinPanel = root.Q<VisualElement>("join-panel");
            m_JoinCodeInput = root.Q<TextField>("join-code-input");
            m_JoinButton = root.Q<Button>("join-button");
            m_ShowJoinButton = root.Q<Button>("show-join-button");
            m_BackToHostButton = root.Q<Button>("back-to-host-button");

            // Event bindings
            if (m_PlayButton != null) m_PlayButton.clicked += OnPlayClicked;
            if (m_LeaveButton != null) m_LeaveButton.clicked += OnLeaveClicked;
            if (m_SwapButton != null) m_SwapButton.clicked += OnSwapClicked;
            if (m_CopyCodeButton != null) m_CopyCodeButton.clicked += OnCopyCodeClicked;
            if (m_JoinButton != null) m_JoinButton.clicked += OnJoinClicked;
            if (m_ShowJoinButton != null) m_ShowJoinButton.clicked += OnShowJoinClicked;
            if (m_BackToHostButton != null) m_BackToHostButton.clicked += OnBackToHostClicked;

            // Welcome message
            if (m_WelcomeLabel != null)
            {
                string name = !string.IsNullOrEmpty(ApiClient.DisplayName)
                    ? ApiClient.DisplayName
                    : ApiClient.Username ?? "Player";
                m_WelcomeLabel.text = $"Xin chào, {name}!";
            }

            // Ẩn join panel ban đầu
            if (m_JoinPanel != null) m_JoinPanel.style.display = DisplayStyle.None;

            // Disable play & swap cho tới khi đủ 2 người
            if (m_PlayButton != null) m_PlayButton.SetEnabled(false);
            if (m_SwapButton != null) m_SwapButton.SetEnabled(false);
        }

        // ─── Auto Create Lobby ──────────────────────────────────────

        private async Task AutoCreateLobby()
        {
            SetStatus("Đang tạo phòng...");

            try
            {
                var lobby = await ApiClient.CreateLobbyAsync();
                m_CurrentLobbyCode = lobby.lobbyCode;
                m_IsHost = true;

                Debug.Log($"[LobbyManager] Auto-created lobby: {lobby.lobbyCode}");
                UpdateLobbyUI(lobby);
                StartPolling();
            }
            catch (ApiException apiEx)
            {
                if (apiEx.StatusCode == 400 && apiEx.ResponseBody != null && apiEx.ResponseBody.Contains("already in a lobby"))
                {
                    SetStatus("Tài khoản đang bị kẹt ở phòng khác (Backend chưa xóa). Vui lòng đợi 30 phút hoặc Reset Backend.");
                    Debug.LogWarning($"[LobbyManager] Backend Error: {apiEx.ResponseBody}");
                    // Cập nhật lại UI code label để báo lỗi
                    if (m_LobbyCodeLabel != null) m_LobbyCodeLabel.text = "LỖI KẸT PHÒNG";
                    // Ngừng retry để tránh bị lặp (hết bị Pause Editor)
                    return;
                }

                SetStatus("Tạo phòng thất bại! Thử lại...");
                Debug.LogError($"[LobbyManager] API Error: {apiEx}");
                
                await Task.Delay(2000);
                if (this != null && gameObject.activeInHierarchy)
                    await AutoCreateLobby();
            }
            catch (System.Exception ex)
            {
                SetStatus("Tạo phòng thất bại! Thử lại...");
                Debug.LogError($"[LobbyManager] Auto-create lobby error: {ex}");

                // Retry after 2 seconds
                await Task.Delay(2000);
                if (this != null && gameObject.activeInHierarchy)
                    await AutoCreateLobby();
            }
        }

        // ─── Join (for the other player) ────────────────────────────

        private void OnShowJoinClicked()
        {
            // Hiện join panel, ẩn lobby panel
            if (m_JoinPanel != null) m_JoinPanel.style.display = DisplayStyle.Flex;
            if (m_RootPanel != null) m_RootPanel.style.display = DisplayStyle.None;
        }

        private void OnBackToHostClicked()
        {
            // Quay lại lobby panel
            if (m_JoinPanel != null) m_JoinPanel.style.display = DisplayStyle.None;
            if (m_RootPanel != null) m_RootPanel.style.display = DisplayStyle.Flex;
        }

        private async void OnJoinClicked()
        {
            string code = m_JoinCodeInput?.value?.Trim().ToUpper();
            if (string.IsNullOrWhiteSpace(code))
            {
                SetStatus("Nhập mã phòng!");
                return;
            }

            if (m_JoinButton != null) m_JoinButton.SetEnabled(false);
            SetStatus("Đang tham gia...");

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

                Debug.Log($"[LobbyManager] Joined lobby: {lobby.lobbyCode}");

                // Quay lại lobby view
                if (m_JoinPanel != null) m_JoinPanel.style.display = DisplayStyle.None;
                if (m_RootPanel != null) m_RootPanel.style.display = DisplayStyle.Flex;

                UpdateLobbyUI(lobby);
                StartPolling();
            }
            catch (ApiException apiEx)
            {
                string errorMsg = apiEx.StatusCode switch
                {
                    404 => "Phòng không tồn tại!",
                    400 => "Phòng đã đầy!",
                    _ => $"Lỗi: {apiEx.Message}"
                };
                SetStatus(errorMsg);
                if (m_JoinButton != null) m_JoinButton.SetEnabled(true);
            }
            catch (System.Exception ex)
            {
                SetStatus("Không thể kết nối!");
                Debug.LogError($"[LobbyManager] Join error: {ex}");
                if (m_JoinButton != null) m_JoinButton.SetEnabled(true);
            }
        }

        // ─── Lobby Actions ──────────────────────────────────────────

        private void OnCopyCodeClicked()
        {
            if (!string.IsNullOrEmpty(m_CurrentLobbyCode))
            {
                GUIUtility.systemCopyBuffer = m_CurrentLobbyCode;
                SetStatus("Đã copy mã phòng!");
                Debug.Log($"[LobbyManager] Copied lobby code: {m_CurrentLobbyCode}");
            }
        }

        private async void OnSwapClicked()
        {
            if (string.IsNullOrEmpty(m_CurrentLobbyCode)) return;
            if (m_SwapButton != null) m_SwapButton.SetEnabled(false);

            try
            {
                var lobby = await ApiClient.SwapRolesAsync(m_CurrentLobbyCode);
                UpdateLobbyUI(lobby);
            }
            catch (System.Exception ex)
            {
                SetStatus("Đổi vai thất bại!");
                Debug.LogError($"[LobbyManager] Swap error: {ex}");
            }
            finally
            {
                if (m_SwapButton != null) m_SwapButton.SetEnabled(true);
            }
        }

        private async void OnPlayClicked()
        {
            if (string.IsNullOrEmpty(m_CurrentLobbyCode)) return;
            if (m_PlayButton != null) m_PlayButton.SetEnabled(false);
            if (m_SwapButton != null) m_SwapButton.SetEnabled(false);
            if (m_LeaveButton != null) m_LeaveButton.SetEnabled(false);
            SetStatus("Đang khởi động game...");

            try
            {
                var result = await ApiClient.ReadyAsync(m_CurrentLobbyCode);
                StopPolling();

                Debug.Log($"[LobbyManager] Game started! SessionId={result.sessionId}, Relay={result.relayJoinCode}");
                SetStatus("🎮 Game bắt đầu!");

                // Ẩn lobby UI
                if (lobbyUI != null)
                    lobbyUI.rootVisualElement.style.display = DisplayStyle.None;

                // TODO: Dùng result.relayJoinCode để connect Netcode qua Unity Relay
                // Khi relay connected, unlock movement
                // if (GameManager.Instance != null)
                // {
                //     GameManager.Instance.StartGameFromLobby();
                // }
            }
            catch (System.Exception ex)
            {
                SetStatus("Bắt đầu game thất bại!");
                Debug.LogError($"[LobbyManager] Play error: {ex}");
                if (m_PlayButton != null) m_PlayButton.SetEnabled(true);
                if (m_SwapButton != null) m_SwapButton.SetEnabled(true);
                if (m_LeaveButton != null) m_LeaveButton.SetEnabled(true);
            }
        }

        private async void OnLeaveClicked()
        {
            if (string.IsNullOrEmpty(m_CurrentLobbyCode)) return;
            StopPolling();
            if (m_LeaveButton != null) m_LeaveButton.SetEnabled(false);

            try
            {
                await ApiClient.LeaveLobbyAsync(m_CurrentLobbyCode);
                Debug.Log($"[LobbyManager] Left lobby: {m_CurrentLobbyCode}");
                m_CurrentLobbyCode = null;

                // Tạo lobby mới
                await AutoCreateLobby();
            }
            catch (System.Exception ex)
            {
                SetStatus("Rời phòng thất bại!");
                Debug.LogError($"[LobbyManager] Leave error: {ex}");
                if (m_LeaveButton != null) m_LeaveButton.SetEnabled(true);
            }
        }

        // ─── Polling ────────────────────────────────────────────────

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

                    // Nếu host đã bấm PLAY (status = InGame), joiner cũng start
                    if (lobby.status == "InGame" && !m_IsHost)
                    {
                        StopPolling();
                        SetStatus("🎮 Game bắt đầu!");

                        if (lobbyUI != null)
                            lobbyUI.rootVisualElement.style.display = DisplayStyle.None;

                        // if (GameManager.Instance != null)
                        //     GameManager.Instance.StartGameFromLobby();

                        break;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[LobbyManager] Poll: {ex.Message}");
                }
            }
        }

        // ─── UI Helpers ─────────────────────────────────────────────

        private void UpdateLobbyUI(ApiClient.LobbyResponseData lobby)
        {
            if (m_LobbyCodeLabel != null)
                m_LobbyCodeLabel.text = lobby.lobbyCode;

            if (m_StatusLabel != null && !string.IsNullOrEmpty(lobby.player2Id))
                m_StatusLabel.text = "Sẵn sàng!";
            else if (m_StatusLabel != null)
                m_StatusLabel.text = "Đang chờ người chơi thứ 2...";

            if (m_DadLabel != null)
                m_DadLabel.text = $"🧔 Ba: {FormatPlayerId(lobby.dadUserId)}";

            if (m_ChildLabel != null)
            {
                m_ChildLabel.text = string.IsNullOrEmpty(lobby.childUserId)
                    ? "👶 Con: Chờ..."
                    : $"👶 Con: {FormatPlayerId(lobby.childUserId)}";
            }

            // Enable PLAY & SWAP chỉ khi đủ 2 người & status = Waiting
            bool canAct = !string.IsNullOrEmpty(lobby.player2Id) && lobby.status == "Waiting";
            if (m_PlayButton != null) m_PlayButton.SetEnabled(canAct);
            if (m_SwapButton != null) m_SwapButton.SetEnabled(canAct);
        }

        private static string FormatPlayerId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "---";
            return id.Length > 8 ? id[..8] + "..." : id;
        }

        private void SetStatus(string msg)
        {
            if (m_StatusLabel != null)
                m_StatusLabel.text = msg;
        }
    }
}
