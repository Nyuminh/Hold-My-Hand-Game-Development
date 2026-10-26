using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

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
        [SerializeField] private float pollInterval = 30f; // increased to reduce lobby request rate

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

        // Bottom right — tutorial
        private VisualElement m_TutorialDialogPanel;
        private Button m_TutorialButton;
        private Button m_SelectDadButton;
        private Button m_SelectChildButton;
        private Button m_ConfirmTutorialButton;
        private Button m_CancelTutorialButton;
        private bool m_TutorialRoleIsDad = true;

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
        private string m_CurrentLobbyId;
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

        private async void Start()
        {
            // RẤT QUAN TRỌNG: Đảm bảo chuột hiện lên để có thể click UI
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    var initOptions = new InitializationOptions();
#if !UNITY_EDITOR
                    initOptions.SetProfile($"Player_{System.Diagnostics.Process.GetCurrentProcess().Id}");
#endif
                    await UnityServices.InitializeAsync(initOptions);
                }
                
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                
                Debug.Log($"[Lobby3D] Signed in to Unity Services: {AuthenticationService.Instance.PlayerId}");

                // Tự động khôi phục phòng chờ nếu vừa từ trận đấu trở về sảnh
                if (!string.IsNullOrEmpty(LobbySessionContext.CurrentLobbyId))
                {
                    _ = RestorePreservedLobbyAsync();
                }

                // Tải trước chỉ số nhân vật từ backend để sẵn sàng trước khi vào scene
                _ = CharacterStatsService.FetchCharacterStatsAsync();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Lobby3D] Unity Services Error: {ex.Message}");
                ShowToast("Lỗi kết nối máy chủ Unity!");
            }
        }

        private async Task RestorePreservedLobbyAsync()
        {
            if (string.IsNullOrEmpty(LobbySessionContext.CurrentLobbyId)) return;

            try
            {
                ShowToast("Đang kết nối lại phòng chờ...");
                await Task.Delay(300); // Tránh rate-limit 429 của UGS khi vừa load scene

                m_IsHost = LobbySessionContext.IsHost;
                Lobby lobby = null;

                try
                {
                    lobby = await LobbyService.Instance.GetLobbyAsync(LobbySessionContext.CurrentLobbyId);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[Lobby3D] GetLobbyAsync failed on restore: {ex.Message}");
                    // Nếu là Client và bị UGS timeout loại khỏi phòng trong trận đấu, tự động rejoin bằng mã phòng
                    if (!m_IsHost && !string.IsNullOrEmpty(LobbySessionContext.CurrentLobbyCode))
                    {
                        try
                        {
                            Debug.Log($"[Lobby3D] Đang tự động kết nối lại phòng {LobbySessionContext.CurrentLobbyCode}...");
                            lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(LobbySessionContext.CurrentLobbyCode);
                        }
                        catch (System.Exception joinEx)
                        {
                            Debug.LogWarning($"[Lobby3D] Auto rejoin failed: {joinEx.Message}");
                        }
                    }
                }

                if (lobby != null)
                {
                    m_CurrentLobbyId = lobby.Id;
                    m_CurrentLobbyCode = lobby.LobbyCode;
                    if (AuthenticationService.Instance.IsSignedIn && !string.IsNullOrEmpty(lobby.HostId))
                    {
                        m_IsHost = (lobby.HostId == AuthenticationService.Instance.PlayerId);
                    }
                    LobbySessionContext.SetCurrentLobby(lobby.Id, lobby.LobbyCode, m_IsHost);

                    if (m_IsHost)
                    {
                        try
                        {
                            var updateOptions = new UpdateLobbyOptions
                            {
                                Data = new Dictionary<string, DataObject>
                                {
                                    { "status", new DataObject(DataObject.VisibilityOptions.Public, "Waiting") },
                                    { "sessionId", new DataObject(DataObject.VisibilityOptions.Public, "") }
                                }
                            };
                            lobby = await LobbyService.Instance.UpdateLobbyAsync(m_CurrentLobbyId, updateOptions);
                        }
                        catch (System.Exception updateEx)
                        {
                            Debug.LogWarning($"[Lobby3D] Host UpdateLobbyAsync warning: {updateEx.Message}");
                        }
                    }

                    UpdateLobbyUI(MapToFakeResponse(lobby));
                    ShowLobbyButtons();
                    StartPolling();
                    ShowToast($"Đã trở lại phòng: {m_CurrentLobbyCode}");
                }
                else
                {
                    Debug.LogWarning("[Lobby3D] Không thể kết nối lại phòng cũ.");
                    LobbySessionContext.Clear();
                    ShowMainButtons();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Lobby3D] RestorePreservedLobbyAsync unhandled exception: {ex.Message}");
                LobbySessionContext.Clear();
                ShowMainButtons();
            }
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

            // Tutorial
            m_TutorialDialogPanel = root.Q<VisualElement>("tutorial-dialog-panel");
            m_TutorialButton = root.Q<Button>("tutorial-button");
            m_SelectDadButton = root.Q<Button>("select-dad-button");
            m_SelectChildButton = root.Q<Button>("select-child-button");
            m_ConfirmTutorialButton = root.Q<Button>("confirm-tutorial-button");
            m_CancelTutorialButton = root.Q<Button>("cancel-tutorial-button");

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
            if (m_TutorialButton != null) m_TutorialButton.clicked += OnTutorialButtonClicked;
            if (m_SelectDadButton != null) m_SelectDadButton.clicked += () => SelectTutorialRole(true);
            if (m_SelectChildButton != null) m_SelectChildButton.clicked += () => SelectTutorialRole(false);
            if (m_ConfirmTutorialButton != null) m_ConfirmTutorialButton.clicked += OnConfirmTutorialClicked;
            if (m_CancelTutorialButton != null) m_CancelTutorialButton.clicked += OnCancelTutorialClicked;

            // Initial state: show main buttons, hide lobby buttons
            ShowMainButtons();

            // Tự động gán âm thanh Hover / Click cho tất cả buttons trong Lobby
            UISoundManager.BindButtons(root);
        }

        private void SetupPreview()
        {
            // Player 1 luôn hiện (nhân vật mình)
            if (player1Preview != null)
            {
                EnsurePreviewAnimator(player1Preview);
                player1Preview.SetActive(true);
            }

            // Player 2 ẩn ban đầu
            if (player2Preview != null)
            {
                EnsurePreviewAnimator(player2Preview);
                player2Preview.SetActive(false);
            }
        }

        private void EnsurePreviewAnimator(GameObject preview)
        {
            if (preview == null) return;
            if (!preview.TryGetComponent<LobbyPreviewAnimator>(out _))
            {
                preview.AddComponent<LobbyPreviewAnimator>();
            }

            // Đảm bảo các mô hình 3D mannequin trong Lobby không bao giờ hiển thị HUD cá nhân
            if (preview.TryGetComponent<UIDocument>(out var uiDoc))
            {
                uiDoc.enabled = false;
            }
            if (preview.TryGetComponent<CoreHUD>(out var hud))
            {
                hud.enabled = false;
            }
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
            if (m_TutorialDialogPanel != null) m_TutorialDialogPanel.style.display = DisplayStyle.None;
            if (m_RoomInfoPanel != null) m_RoomInfoPanel.style.display = DisplayStyle.None;
        }

        private void ShowLobbyButtons()
        {
            if (m_MainButtonsPanel != null) m_MainButtonsPanel.style.display = DisplayStyle.None;
            if (m_LobbyButtonsPanel != null) m_LobbyButtonsPanel.style.display = DisplayStyle.Flex;
            if (m_JoinCodePanel != null) m_JoinCodePanel.style.display = DisplayStyle.None;
            if (m_TutorialDialogPanel != null) m_TutorialDialogPanel.style.display = DisplayStyle.None;
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
        #region Tutorial Handlers

        private void OnTutorialButtonClicked()
        {
            UISoundManager.PlayClick();
            if (m_MainButtonsPanel != null) m_MainButtonsPanel.style.display = DisplayStyle.None;
            if (m_TutorialDialogPanel != null) m_TutorialDialogPanel.style.display = DisplayStyle.Flex;
            SelectTutorialRole(m_TutorialRoleIsDad);
        }

        private void SelectTutorialRole(bool isDad)
        {
            m_TutorialRoleIsDad = isDad;
            AmIDad = isDad;
            UISoundManager.PlayClick();

            if (m_SelectDadButton != null)
            {
                if (isDad) m_SelectDadButton.AddToClassList("role-card-active");
                else m_SelectDadButton.RemoveFromClassList("role-card-active");
            }

            if (m_SelectChildButton != null)
            {
                if (!isDad) m_SelectChildButton.AddToClassList("role-card-active");
                else m_SelectChildButton.RemoveFromClassList("role-card-active");
            }

            // Cập nhật mô hình preview 3D tương ứng trong Lobby
            if (player1Preview != null) player1Preview.SetActive(isDad);
            if (player2Preview != null) player2Preview.SetActive(!isDad);
        }

        private void OnCancelTutorialClicked()
        {
            UISoundManager.PlayCancel();
            if (m_TutorialDialogPanel != null) m_TutorialDialogPanel.style.display = DisplayStyle.None;
            if (m_MainButtonsPanel != null) m_MainButtonsPanel.style.display = DisplayStyle.Flex;
            SetupPreview();
        }

        private async void OnConfirmTutorialClicked()
        {
            UISoundManager.PlayConfirm();
            AmIDad = m_TutorialRoleIsDad;
            StopPolling();
            ShowToast("🎮 Đang nạp chỉ số và vào chế độ Tập Luyện...");
            
            // Tải chỉ số từ backend trước khi vào Scene Tutorial
            await CharacterStatsService.EnsureLoadedAsync();

            LobbySessionContext.Clear();
            SceneManager.LoadScene("Tutorial");
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
                var options = new CreateLobbyOptions {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject> {
                        { "dadUserId", new DataObject(DataObject.VisibilityOptions.Public, AuthenticationService.Instance.PlayerId) },
                        { "childUserId", new DataObject(DataObject.VisibilityOptions.Public, "") },
                        { "status", new DataObject(DataObject.VisibilityOptions.Public, "Waiting") }
                    }
                };
                var lobby = await LobbyService.Instance.CreateLobbyAsync("HoldMyHand", 2, options);
                
                m_CurrentLobbyId = lobby.Id;
                m_CurrentLobbyCode = lobby.LobbyCode;
                m_IsHost = true;
                AmIDad = true; // Host tạo phòng mặc định đóng vai Ba
                LobbySessionContext.SetCurrentLobby(lobby.Id, lobby.LobbyCode, true);

                Debug.Log($"[Lobby3D] Created Unity lobby: {lobby.LobbyCode}");
                UpdateLobbyUI(MapToFakeResponse(lobby));
                ShowLobbyButtons();
                StartPolling();
                UISoundManager.PlaySuccess();
                ShowToast("Phòng đã được tạo!");
            }
            catch (LobbyServiceException ex)
            {
                UISoundManager.PlayError();
                ShowToast($"Tạo phòng thất bại: {ex.Message}");
                Debug.LogError($"[Lobby3D] Create lobby error: {ex}");
            }
            finally
            {
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
            UISoundManager.PlayCancel();
            HideJoinCodePanel();
        }

        private async void OnConfirmJoinClicked()
        {
            string code = m_JoinCodeInput?.value?.Trim().Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "").ToUpper();
            if (string.IsNullOrWhiteSpace(code))
            {
                UISoundManager.PlayError();
                ShowToast("Nhập mã phòng!");
                return;
            }

            if (m_ConfirmJoinButton != null) m_ConfirmJoinButton.SetEnabled(false);
            ShowToast("Đang tham gia...");

            try
            {
                if (!string.IsNullOrEmpty(m_CurrentLobbyId))
                {
                    StopPolling();
                    try { await LobbyService.Instance.RemovePlayerAsync(m_CurrentLobbyId, AuthenticationService.Instance.PlayerId); } catch { }
                }

                var lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code);
                
                m_CurrentLobbyId = lobby.Id;
                m_CurrentLobbyCode = lobby.LobbyCode;
                m_IsHost = false;
                LobbySessionContext.SetCurrentLobby(lobby.Id, lobby.LobbyCode, false);

                Debug.Log($"[Lobby3D] Joined Unity lobby: {lobby.LobbyCode}");
                HideJoinCodePanel();
                UpdateLobbyUI(MapToFakeResponse(lobby));
                ShowLobbyButtons();
                StartPolling();
                UISoundManager.PlaySuccess();
                ShowToast("Đã vào phòng!");
            }
            catch (LobbyServiceException ex)
            {
                UISoundManager.PlayError();
                if (ex.Reason == LobbyExceptionReason.LobbyNotFound || ex.ErrorCode == 16010 || (ex.Message != null && ex.Message.ToLower().Contains("not found")))
                {
                    ShowToast("Không tìm thấy phòng! Hãy kiểm tra lại mã hoặc phòng đã hết hạn.");
                    Debug.LogWarning($"[Lobby3D] Tham gia phòng '{code}' thất bại: Phòng không tồn tại hoặc đã hết hạn (404 Not Found).");
                }
                else
                {
                    ShowToast($"Lỗi vào phòng: {ex.Message}");
                    Debug.LogWarning($"[Lobby3D] Join lobby error: {ex.Message}");
                }
            }
            finally
            {
                if (m_ConfirmJoinButton != null) m_ConfirmJoinButton.SetEnabled(true);
            }
        }

        private void OnCopyCodeClicked()
        {
            if (!string.IsNullOrEmpty(m_CurrentLobbyCode))
            {
                GUIUtility.systemCopyBuffer = m_CurrentLobbyCode;
                UISoundManager.PlayClick();
                ShowToast("Đã copy mã phòng!");
            }
        }

        private async void OnSwapRoleClicked()
        {
            if (string.IsNullOrEmpty(m_CurrentLobbyId)) return;
            if (m_SwapRoleButton != null) m_SwapRoleButton.SetEnabled(false);

            try
            {
                var lobby = await LobbyService.Instance.GetLobbyAsync(m_CurrentLobbyId);
                var currentDad = lobby.Data.ContainsKey("dadUserId") ? lobby.Data["dadUserId"].Value : "";
                var currentChild = lobby.Data.ContainsKey("childUserId") ? lobby.Data["childUserId"].Value : "";

                // Nếu currentChild đang rỗng trong Data, tìm trong lobby.Players người chơi thứ 2
                if (string.IsNullOrEmpty(currentChild) && lobby.Players.Count > 1)
                {
                    foreach (var p in lobby.Players)
                    {
                        if (p.Id != currentDad)
                        {
                            currentChild = p.Id;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(currentDad) && lobby.Players.Count > 0)
                {
                    currentDad = lobby.HostId;
                }

                var options = new UpdateLobbyOptions {
                    Data = new Dictionary<string, DataObject> {
                        { "dadUserId", new DataObject(DataObject.VisibilityOptions.Public, currentChild) },
                        { "childUserId", new DataObject(DataObject.VisibilityOptions.Public, currentDad) }
                    }
                };
                lobby = await LobbyService.Instance.UpdateLobbyAsync(m_CurrentLobbyId, options);
                AmIDad = (AuthenticationService.Instance.PlayerId == currentChild);

                UpdateLobbyUI(MapToFakeResponse(lobby));
                UISoundManager.PlaySwapRole();
                ShowToast("Đã đổi vai!");
            }
            catch (System.Exception ex)
            {
                UISoundManager.PlayError();
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
            if (string.IsNullOrEmpty(m_CurrentLobbyId)) return;
            StopPolling();
            if (m_LeaveRoomButton != null) m_LeaveRoomButton.SetEnabled(false);

            try
            {
                await LobbyService.Instance.RemovePlayerAsync(m_CurrentLobbyId, AuthenticationService.Instance.PlayerId);
                Debug.Log($"[Lobby3D] Left Unity lobby.");
                
                LobbySessionContext.Clear();
                m_CurrentLobbyId = null;
                m_CurrentLobbyCode = null;

                if (player2Preview != null) player2Preview.SetActive(false);

                ShowMainButtons();
                UISoundManager.PlayCancel();
                ShowToast("Đã rời phòng.");
            }
            catch (System.Exception ex)
            {
                UISoundManager.PlayError();
                ShowToast("Rời phòng thất bại!");
                Debug.LogError($"[Lobby3D] Leave error: {ex}");
            }
            finally
            {
                if (m_CreateRoomButton != null) m_CreateRoomButton.SetEnabled(true);
                if (m_LeaveRoomButton != null) m_LeaveRoomButton.SetEnabled(true);
            }
        }

        public static bool AmIDad = true; // Lưu role của bản thân để dùng lúc Spawn

        private async void OnPlayClicked()
        {
            if (string.IsNullOrEmpty(m_CurrentLobbyId)) return;
            UISoundManager.PlayConfirm();
            if (m_PlayButton != null) m_PlayButton.SetEnabled(false);
            if (m_SwapRoleButton != null) m_SwapRoleButton.SetEnabled(false);
            if (m_LeaveRoomButton != null) m_LeaveRoomButton.SetEnabled(false);
            ShowToast("Đang chuẩn bị vào game...");

            try
            {
                var lobby = await LobbyService.Instance.GetLobbyAsync(m_CurrentLobbyId);
                string dadId = lobby.Data.ContainsKey("dadUserId") ? lobby.Data["dadUserId"].Value : "";
                if (string.IsNullOrEmpty(dadId)) dadId = lobby.HostId;
                AmIDad = (AuthenticationService.Instance.PlayerId == dadId);

                var options = new UpdateLobbyOptions {
                    Data = new Dictionary<string, DataObject> {
                        { "status", new DataObject(DataObject.VisibilityOptions.Public, "InGame") }
                    }
                };
                await LobbyService.Instance.UpdateLobbyAsync(m_CurrentLobbyId, options);
                StopPolling();

                ShowToast("🎮 Đang nạp chỉ số và Load Game Scene...");

                // Đảm bảo chỉ số nhân vật đã được nạp từ Backend trước khi load scene
                await CharacterStatsService.EnsureLoadedAsync();

                // Sinh ra bộ tự động Start Session cho Host mang sang Game Scene
                GameObject bootstrapper = new GameObject("AutoSessionBootstrapper");
                DontDestroyOnLoad(bootstrapper);
                var starter = bootstrapper.AddComponent<AutoSessionStarter>();
                starter.Setup(true, m_CurrentLobbyId);

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

        private void OnRefreshSessionsClicked()
        {
            if (m_RefreshSessionsButton != null) m_RefreshSessionsButton.SetEnabled(false);
            ShowToast("Đang tải danh sách phòng...");

            try
            {
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
            while (!ct.IsCancellationRequested && !string.IsNullOrEmpty(m_CurrentLobbyId))
            {
                try { await Task.Delay((int)(pollInterval * 1000), ct); }
                catch (TaskCanceledException) { break; }
                if (ct.IsCancellationRequested) break;

                try
                {
                    var lobby = await LobbyService.Instance.GetLobbyAsync(m_CurrentLobbyId);
                    UpdateLobbyUI(MapToFakeResponse(lobby));

                    if (lobby.Data.ContainsKey("status") && lobby.Data["status"].Value == "InGame" && !m_IsHost)
                    {
                        StopPolling();
                        LobbySessionContext.StopHeartbeat(); // Dừng keep-alive trước khi vào game scene
                        ShowToast("🎮 Đang Load Game Scene...");

                        string dadId = lobby.Data.ContainsKey("dadUserId") ? lobby.Data["dadUserId"].Value : "";
                        if (string.IsNullOrEmpty(dadId)) dadId = lobby.HostId;
                        AmIDad = (AuthenticationService.Instance.PlayerId == dadId);

                        // Tải trước chỉ số nhân vật cho Client trước khi load scene
                        await CharacterStatsService.EnsureLoadedAsync();

                        // Sinh ra bộ tự động Start Session cho Client mang sang Game Scene
                        GameObject bootstrapper = new GameObject("AutoSessionBootstrapper");
                        DontDestroyOnLoad(bootstrapper);
                        var starter = bootstrapper.AddComponent<AutoSessionStarter>();
                        starter.Setup(false, m_CurrentLobbyId);

                        SceneManager.LoadScene(gameSceneName);
                        
                        break;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[Lobby3D] Poll Unity Lobby: {ex.Message}");
                }
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────
        #region UI Helpers

        private ApiClient.LobbyResponseData MapToFakeResponse(Lobby lobby)
        {
            var p2Id = lobby.Players.Count > 1 ? lobby.Players[1].Id : null;
            
            string dadId = lobby.Data.ContainsKey("dadUserId") ? lobby.Data["dadUserId"].Value : "";
            string childId = lobby.Data.ContainsKey("childUserId") ? lobby.Data["childUserId"].Value : "";

            // Nếu 1 trong 2 role đang trống, tự động điền bằng Host hoặc P2
            if (string.IsNullOrEmpty(dadId)) 
            {
                if (string.IsNullOrEmpty(childId) || childId != lobby.HostId) dadId = lobby.HostId;
                else dadId = p2Id;
            }
            if (string.IsNullOrEmpty(childId))
            {
                if (string.IsNullOrEmpty(dadId) || dadId != lobby.HostId) childId = lobby.HostId;
                else childId = p2Id;
            }

            return new ApiClient.LobbyResponseData
            {
                lobbyCode = lobby.LobbyCode,
                player1Id = lobby.HostId,
                player2Id = p2Id,
                dadUserId = dadId,
                childUserId = childId,
                status = lobby.Data.ContainsKey("status") ? lobby.Data["status"].Value : "Waiting"
            };
        }

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
                string myId = AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : null;
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

            // Player Previews:
            // player1Preview = Dad model
            // player2Preview = Child model
            bool hasDad = !string.IsNullOrEmpty(lobby.dadUserId);
            bool hasChild = !string.IsNullOrEmpty(lobby.childUserId);
            
            if (player1Preview != null)
            {
                EnsurePreviewAnimator(player1Preview);
                player1Preview.SetActive(hasDad);
            }

            if (player2Preview != null)
            {
                EnsurePreviewAnimator(player2Preview);
                player2Preview.SetActive(hasChild);
            }

            // PLAY button — chỉ enable khi đủ 2 người + status Waiting
            bool canPlay = hasPlayer2 && lobby.status == "Waiting";
            if (m_PlayButton != null)
            {
                m_PlayButton.SetEnabled(canPlay);
                if (canPlay)
                {
                    m_PlayButton.style.backgroundColor = new StyleColor(new Color(0.90f, 0.66f, 0.16f)); // Vàng hoàng kim
                    m_PlayButton.style.color = new StyleColor(new Color(0.08f, 0.06f, 0.02f)); // Chữ tối màu
                }
                else
                {
                    m_PlayButton.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.1f));
                    m_PlayButton.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.3f));
                }
            }

            // Swap: Chỉ Host mới được phép Swap (vì chỉ Host mới có quyền update Unity Lobby)
            if (m_SwapRoleButton != null)
                m_SwapRoleButton.SetEnabled(m_IsHost);
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
