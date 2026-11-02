using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
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
        [Tooltip("Canvas uGUI phủ lên Lobby 3D")]
        [SerializeField] private Canvas lobbyCanvas;

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

        [Header("Voice Backend")]
        [SerializeField] private string voiceServerUrl = "wss://hold-my-hand-backend.vercel.app/voice";

        // ─── UI Elements ────────────────────────────────────────
        // Top
        private TextMeshProUGUI m_PlayerNameLabel;
        private TextMeshProUGUI m_PlayerRoleLabel;
        private GameObject m_RoomInfoPanel;
        private TextMeshProUGUI m_RoomCodeLabel;
        private TextMeshProUGUI m_RoomStatusLabel;
        private Button m_CopyCodeButton;
        private GameObject m_RemoteProfileCard;
        private TextMeshProUGUI m_RemotePlayerLabel;
        private TextMeshProUGUI m_LocalSpeakingIndicator;
        private TextMeshProUGUI m_RemoteSpeakingIndicator;

        // Bottom right — main
        private GameObject m_MainButtonsPanel;
        private Button m_CreateRoomButton;
        private Button m_JoinRoomButton;
        private Button m_BrowseSessionsButton;

        // Bottom right — tutorial
        private GameObject m_TutorialDialogPanel;
        private Button m_TutorialButton;
        private Button m_SelectDadButton;
        private Button m_SelectChildButton;
        private Button m_ConfirmTutorialButton;
        private Button m_CancelTutorialButton;
        private bool m_TutorialRoleIsDad = true;

        // Bottom right — join code
        private GameObject m_JoinCodePanel;
        private TMP_InputField m_JoinCodeInput;
        private Button m_ConfirmJoinButton;
        private Button m_CancelJoinButton;

        // Bottom right — in-lobby
        private GameObject m_LobbyButtonsPanel;
        private Button m_PlayButton;
        private Button m_SwapRoleButton;
        private Button m_LeaveRoomButton;
        private Button m_VoiceMicButton;
        private Button m_VoiceSettingsButton;
        private GameObject m_VoiceSettingsPanel;
        private Button m_VoicePttButton;
        private Button m_VoiceAutoButton;
        private Slider m_VoiceThresholdSlider;
        private Slider m_VoiceVolumeSlider;

        // Bottom left — session list
        private GameObject m_SessionListPanel;
        private GameObject m_BottomLeftSpacer;
        private ScrollRect m_SessionScroll;
        private TextMeshProUGUI m_NoSessionsLabel;
        private Button m_RefreshSessionsButton;
        private Button m_CloseSessionsButton;

        private GameObject m_ChatPanel;
        private ScrollRect m_ChatScroll;
        private TMP_InputField m_ChatInput;
        private Button m_ChatSendButton;
        private readonly HashSet<string> m_SeenChatMessages = new HashSet<string>();
        private readonly List<ChatMessage> m_OwnChatMessages = new List<ChatMessage>();
        private bool m_ChatSending;

        [Serializable]
        private class ChatMessage
        {
            public string id;
            public string sender;
            public string content;
        }

        [Serializable]
        private class ChatFeed
        {
            public ChatMessage[] messages;
        }

        // Center toast
        private TextMeshProUGUI m_ToastLabel;
        private GameObject m_ToastPanel;

        // ─── State ──────────────────────────────────────────────
        private string m_CurrentLobbyCode;
        private string m_CurrentLobbyId;
        private bool m_IsHost;
        private CancellationTokenSource m_PollCts;

        // ────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void OnEnable()
        {
            if (lobbyCanvas == null)
            {
                Debug.LogError("[Lobby3DManager] lobbyCanvas is not assigned.", this);
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
                    ReceiveChatMessages(lobby);
                    StartVoiceForCurrentLobby();
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
            if (VoiceChatManager.Instance != null) VoiceChatManager.Instance.SuppressPushToTalk = false;
        }

        private void OnDestroy()
        {
            StopPolling();
        }

        #endregion

        // ────────────────────────────────────────────────────────
        #region UI Setup

        private T FindUI<T>(string name) where T : Component
        {
            foreach (var item in lobbyCanvas.GetComponentsInChildren<T>(true))
                if (item.gameObject.name == name) return item;
            Debug.LogError($"[Lobby3DManager] Missing UI component: {name}", this);
            return null;
        }

        private GameObject FindPanel(string name)
        {
            foreach (var item in lobbyCanvas.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
            Debug.LogError($"[Lobby3DManager] Missing UI panel: {name}", this);
            return null;
        }

        private void SetupUI()
        {
            m_PlayerNameLabel = FindUI<TextMeshProUGUI>("player-name-label");
            m_PlayerRoleLabel = FindUI<TextMeshProUGUI>("player-role-label");
            m_RoomInfoPanel = FindPanel("room-info-panel");
            m_RoomCodeLabel = FindUI<TextMeshProUGUI>("room-code-label");
            m_RoomStatusLabel = FindUI<TextMeshProUGUI>("room-status-label");
            m_CopyCodeButton = FindUI<Button>("copy-code-button");
            m_RemoteProfileCard = FindPanel("remote-profile-card");
            m_RemotePlayerLabel = FindUI<TextMeshProUGUI>("remote-player-label");
            m_LocalSpeakingIndicator = FindUI<TextMeshProUGUI>("local-speaking-indicator");
            m_RemoteSpeakingIndicator = FindUI<TextMeshProUGUI>("remote-speaking-indicator");
            m_MainButtonsPanel = FindPanel("main-buttons-panel");
            m_CreateRoomButton = FindUI<Button>("create-room-button");
            m_JoinRoomButton = FindUI<Button>("join-room-button");
            m_BrowseSessionsButton = FindUI<Button>("browse-sessions-button");
            m_JoinCodePanel = FindPanel("join-code-panel");
            m_JoinCodeInput = FindUI<TMP_InputField>("join-code-input");
            m_ConfirmJoinButton = FindUI<Button>("confirm-join-button");
            m_CancelJoinButton = FindUI<Button>("cancel-join-button");
            m_LobbyButtonsPanel = FindPanel("lobby-buttons-panel");
            m_PlayButton = FindUI<Button>("play-button");
            m_SwapRoleButton = FindUI<Button>("swap-role-button");
            m_LeaveRoomButton = FindUI<Button>("leave-room-button");
            m_VoiceMicButton = FindUI<Button>("voice-mic-button");
            m_VoiceSettingsButton = FindUI<Button>("voice-settings-button");
            m_VoiceSettingsPanel = FindPanel("voice-settings-panel");
            m_VoicePttButton = FindUI<Button>("voice-ptt-button");
            m_VoiceAutoButton = FindUI<Button>("voice-auto-button");
            m_VoiceThresholdSlider = FindUI<Slider>("voice-threshold-slider");
            m_VoiceVolumeSlider = FindUI<Slider>("voice-volume-slider");
            m_SessionListPanel = FindPanel("session-list-panel");
            m_BottomLeftSpacer = FindPanel("bottom-left-spacer");
            m_SessionScroll = FindUI<ScrollRect>("session-scroll");
            m_NoSessionsLabel = FindUI<TextMeshProUGUI>("no-sessions-label");
            m_RefreshSessionsButton = FindUI<Button>("refresh-sessions-button");
            m_CloseSessionsButton = FindUI<Button>("close-sessions-button");
            m_ChatPanel = FindPanel("chat-panel");
            m_ChatScroll = FindUI<ScrollRect>("chat-scroll");
            m_ChatInput = FindUI<TMP_InputField>("chat-input");
            m_ChatSendButton = FindUI<Button>("chat-send-button");
            m_ToastLabel = FindUI<TextMeshProUGUI>("toast-label");
            m_ToastPanel = FindPanel("toast-panel");
            if (m_ToastLabel != null) m_ToastLabel.text = "";
            m_TutorialDialogPanel = FindPanel("tutorial-dialog-panel");
            m_TutorialButton = FindUI<Button>("tutorial-button");
            m_SelectDadButton = FindUI<Button>("select-dad-button");
            m_SelectChildButton = FindUI<Button>("select-child-button");
            m_ConfirmTutorialButton = FindUI<Button>("confirm-tutorial-button");
            m_CancelTutorialButton = FindUI<Button>("cancel-tutorial-button");

            if (m_JoinCodeInput != null)
            {
                m_JoinCodeInput.characterLimit = 6;
                m_JoinCodeInput.contentType = TMP_InputField.ContentType.Alphanumeric;
            }
            if (m_ChatInput != null) m_ChatInput.characterLimit = 240;
            if (m_CreateRoomButton != null) m_CreateRoomButton.onClick.AddListener(OnCreateRoomClicked);
            if (m_JoinRoomButton != null) m_JoinRoomButton.onClick.AddListener(OnJoinRoomClicked);
            if (m_BrowseSessionsButton != null) m_BrowseSessionsButton.onClick.AddListener(OnBrowseSessionsClicked);
            if (m_ConfirmJoinButton != null) m_ConfirmJoinButton.onClick.AddListener(OnConfirmJoinClicked);
            if (m_CancelJoinButton != null) m_CancelJoinButton.onClick.AddListener(OnCancelJoinClicked);
            if (m_CopyCodeButton != null) m_CopyCodeButton.onClick.AddListener(OnCopyCodeClicked);
            if (m_PlayButton != null) m_PlayButton.onClick.AddListener(OnPlayClicked);
            if (m_SwapRoleButton != null) m_SwapRoleButton.onClick.AddListener(OnSwapRoleClicked);
            if (m_LeaveRoomButton != null) m_LeaveRoomButton.onClick.AddListener(OnLeaveRoomClicked);
            if (m_VoiceMicButton != null) m_VoiceMicButton.onClick.AddListener(OnVoiceMicClicked);
            if (m_VoiceSettingsButton != null) m_VoiceSettingsButton.onClick.AddListener(OnVoiceSettingsClicked);
            if (m_VoicePttButton != null) m_VoicePttButton.onClick.AddListener(() => SetVoiceMode(true));
            if (m_VoiceAutoButton != null) m_VoiceAutoButton.onClick.AddListener(() => SetVoiceMode(false));
            if (m_VoiceThresholdSlider != null) m_VoiceThresholdSlider.onValueChanged.AddListener(value => VoiceChatManager.EnsureInstance().VoiceThreshold = value);
            if (m_VoiceVolumeSlider != null) m_VoiceVolumeSlider.onValueChanged.AddListener(value => VoiceChatManager.EnsureInstance().PlaybackVolume = value);
            if (m_RefreshSessionsButton != null) m_RefreshSessionsButton.onClick.AddListener(OnRefreshSessionsClicked);
            if (m_CloseSessionsButton != null) m_CloseSessionsButton.onClick.AddListener(OnCloseSessionsClicked);
            if (m_ChatSendButton != null) m_ChatSendButton.onClick.AddListener(OnChatSendClicked);
            if (m_ChatInput != null) m_ChatInput.onSubmit.AddListener(_ => OnChatSendClicked());
            if (m_TutorialButton != null) m_TutorialButton.onClick.AddListener(OnTutorialButtonClicked);
            if (m_SelectDadButton != null) m_SelectDadButton.onClick.AddListener(() => SelectTutorialRole(true));
            if (m_SelectChildButton != null) m_SelectChildButton.onClick.AddListener(() => SelectTutorialRole(false));
            if (m_ConfirmTutorialButton != null) m_ConfirmTutorialButton.onClick.AddListener(OnConfirmTutorialClicked);
            if (m_CancelTutorialButton != null) m_CancelTutorialButton.onClick.AddListener(OnCancelTutorialClicked);
            ShowMainButtons();
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

            // Đảm bảo các mô hình 3D mannequin trong Lobby không hiển thị HUD cá nhân.
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
            if (m_ChatPanel != null) m_ChatPanel.SetActive(false);
            if (m_RemoteProfileCard != null) m_RemoteProfileCard.SetActive(false);
            if (m_VoiceSettingsPanel != null) m_VoiceSettingsPanel.SetActive(false);
            if (m_MainButtonsPanel != null) m_MainButtonsPanel.SetActive(true);
            if (m_LobbyButtonsPanel != null) m_LobbyButtonsPanel.SetActive(false);
            if (m_JoinCodePanel != null) m_JoinCodePanel.SetActive(false);
            if (m_TutorialDialogPanel != null) m_TutorialDialogPanel.SetActive(false);
            if (m_RoomInfoPanel != null) m_RoomInfoPanel.SetActive(false);
        }

        private void ShowLobbyButtons()
        {
            if (m_ChatPanel != null) m_ChatPanel.SetActive(true);
            if (m_ChatScroll != null) ClearScroll(m_ChatScroll);
            m_SeenChatMessages.Clear();
            m_OwnChatMessages.Clear();
            if (m_SessionListPanel != null) m_SessionListPanel.SetActive(false);
            if (m_BottomLeftSpacer != null) m_BottomLeftSpacer.SetActive(true);
            var voice = VoiceChatManager.EnsureInstance();
            if (m_VoiceThresholdSlider != null) m_VoiceThresholdSlider.value = voice.VoiceThreshold;
            if (m_VoiceVolumeSlider != null) m_VoiceVolumeSlider.value = voice.PlaybackVolume;
            RefreshVoiceUI();
            if (m_MainButtonsPanel != null) m_MainButtonsPanel.SetActive(false);
            if (m_LobbyButtonsPanel != null) m_LobbyButtonsPanel.SetActive(true);
            if (m_JoinCodePanel != null) m_JoinCodePanel.SetActive(false);
            if (m_TutorialDialogPanel != null) m_TutorialDialogPanel.SetActive(false);
            if (m_RoomInfoPanel != null) m_RoomInfoPanel.SetActive(true);
        }

        private void ShowJoinCodePanel()
        {
            if (m_JoinCodePanel != null) m_JoinCodePanel.SetActive(true);
        }

        private void HideJoinCodePanel()
        {
            if (m_JoinCodePanel != null) m_JoinCodePanel.SetActive(false);
        }

        #endregion

        // ────────────────────────────────────────────────────────
        #region Tutorial Handlers

        private void OnTutorialButtonClicked()
        {
            UISoundManager.PlayClick();
            if (m_MainButtonsPanel != null) m_MainButtonsPanel.SetActive(false);
            if (m_TutorialDialogPanel != null) m_TutorialDialogPanel.SetActive(true);
            SelectTutorialRole(m_TutorialRoleIsDad);
        }

        private void SelectTutorialRole(bool isDad)
        {
            m_TutorialRoleIsDad = isDad;
            AmIDad = isDad;
            UISoundManager.PlayClick();

            if (m_SelectDadButton != null)
            {
                if (isDad) SetSelected(m_SelectDadButton, true);
                else SetSelected(m_SelectDadButton, false);
            }

            if (m_SelectChildButton != null)
            {
                if (!isDad) SetSelected(m_SelectChildButton, true);
                else SetSelected(m_SelectChildButton, false);
            }

            // Cập nhật mô hình preview 3D tương ứng trong Lobby
            if (player1Preview != null) player1Preview.SetActive(isDad);
            if (player2Preview != null) player2Preview.SetActive(!isDad);
        }

        private void OnCancelTutorialClicked()
        {
            UISoundManager.PlayCancel();
            if (m_TutorialDialogPanel != null) m_TutorialDialogPanel.SetActive(false);
            if (m_MainButtonsPanel != null) m_MainButtonsPanel.SetActive(true);
            SetupPreview();
        }

        private async void OnConfirmTutorialClicked()
        {
            UISoundManager.PlayConfirm();
            AmIDad = m_TutorialRoleIsDad;
            StopPolling();
            ShowToast("Đang nạp chỉ số và vào chế độ Tập Luyện...");
            
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
            if (m_CreateRoomButton != null) m_CreateRoomButton.interactable = false;
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
                ReceiveChatMessages(lobby);
                StartVoiceForCurrentLobby();
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
                if (m_CreateRoomButton != null) m_CreateRoomButton.interactable = true;
            }
        }

        private void OnJoinRoomClicked()
        {
            ShowJoinCodePanel();
            if (m_JoinCodeInput != null) m_JoinCodeInput.text = "";
        }

        private void OnCancelJoinClicked()
        {
            UISoundManager.PlayCancel();
            HideJoinCodePanel();
        }

        private async void OnConfirmJoinClicked()
        {
            string code = m_JoinCodeInput?.text?.Trim().Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "").ToUpper();
            if (string.IsNullOrWhiteSpace(code))
            {
                UISoundManager.PlayError();
                ShowToast("Nhập mã phòng!");
                return;
            }

            if (m_ConfirmJoinButton != null) m_ConfirmJoinButton.interactable = false;
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
                ReceiveChatMessages(lobby);
                StartVoiceForCurrentLobby();
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
                if (m_ConfirmJoinButton != null) m_ConfirmJoinButton.interactable = true;
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
            if (m_SwapRoleButton != null) m_SwapRoleButton.interactable = false;

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
                if (m_SwapRoleButton != null) m_SwapRoleButton.interactable = true;
            }
        }

        private async void OnLeaveRoomClicked()
        {
            if (string.IsNullOrEmpty(m_CurrentLobbyId)) return;
            StopPolling();
            if (m_LeaveRoomButton != null) m_LeaveRoomButton.interactable = false;

            try
            {
                await LobbyService.Instance.RemovePlayerAsync(m_CurrentLobbyId, AuthenticationService.Instance.PlayerId);
                Debug.Log($"[Lobby3D] Left Unity lobby.");
                
                LobbySessionContext.Clear();
                VoiceChatManager.Instance?.LeaveLobby();
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
                if (m_CreateRoomButton != null) m_CreateRoomButton.interactable = true;
                if (m_LeaveRoomButton != null) m_LeaveRoomButton.interactable = true;
            }
        }

        public static bool AmIDad = true; // Lưu role của bản thân để dùng lúc Spawn

        private async void OnPlayClicked()
        {
            if (string.IsNullOrEmpty(m_CurrentLobbyId)) return;
            UISoundManager.PlayConfirm();
            if (m_PlayButton != null) m_PlayButton.interactable = false;
            if (m_SwapRoleButton != null) m_SwapRoleButton.interactable = false;
            if (m_LeaveRoomButton != null) m_LeaveRoomButton.interactable = false;
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

                ShowToast("Đang nạp chỉ số và Load Game Scene...");

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
                if (m_PlayButton != null) m_PlayButton.interactable = true;
                if (m_SwapRoleButton != null) m_SwapRoleButton.interactable = true;
                if (m_LeaveRoomButton != null) m_LeaveRoomButton.interactable = true;
            }
        }

        // ─── Session Browser ────────────────────────────────────
        private void OnBrowseSessionsClicked()
        {
            if (m_SessionListPanel != null) m_SessionListPanel.SetActive(true);
            if (m_BottomLeftSpacer != null) m_BottomLeftSpacer.SetActive(false);
            OnRefreshSessionsClicked();
        }

        private void OnCloseSessionsClicked()
        {
            if (m_SessionListPanel != null) m_SessionListPanel.SetActive(false);
            if (m_BottomLeftSpacer != null) m_BottomLeftSpacer.SetActive(true);
        }

        private void OnRefreshSessionsClicked()
        {
            if (m_RefreshSessionsButton != null) m_RefreshSessionsButton.interactable = false;
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
                if (m_RefreshSessionsButton != null) m_RefreshSessionsButton.interactable = true;
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────
        #region Polling

        private void OnVoiceMicClicked()
        {
            var voice = VoiceChatManager.EnsureInstance();
            voice.SetMuted(!voice.IsMuted);
            if (voice.IsMuted && Microphone.devices.Length == 0) ShowToast("Không tìm thấy micro.");
            RefreshVoiceUI();
        }

        private void OnVoiceSettingsClicked()
        {
            if (m_VoiceSettingsPanel == null) return;
            m_VoiceSettingsPanel.SetActive(!m_VoiceSettingsPanel.activeSelf);
        }

        private void SetVoiceMode(bool pushToTalk)
        {
            VoiceChatManager.EnsureInstance().PushToTalk = pushToTalk;
            RefreshVoiceUI();
        }

        private void Update()
        {
            var voice = VoiceChatManager.Instance;
            var focused = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if (voice != null) voice.SuppressPushToTalk = focused != null && m_ChatInput != null && (focused == m_ChatInput.gameObject || focused.transform.IsChildOf(m_ChatInput.transform));
            RefreshVoiceUI();
        }

        private void RefreshVoiceUI()
        {
            var voice = VoiceChatManager.Instance;
            if (voice == null) return;
            bool speaking = voice.IsLocalSpeaking;
            bool blink = Mathf.Sin(Time.unscaledTime * 14f) > -0.2f;
            if (m_VoiceMicButton != null)
            {
                SetButtonText(m_VoiceMicButton, voice.IsMuted ? "MIC: TẮT" : voice.IsConnected ? speaking ? "MIC: ĐANG NÓI" : "MIC: BẬT" : "MIC: ĐANG KẾT NỐI");
            }
            if (m_LocalSpeakingIndicator != null) m_LocalSpeakingIndicator.gameObject.SetActive(speaking && blink);
            if (m_RemoteSpeakingIndicator != null) m_RemoteSpeakingIndicator.gameObject.SetActive(voice.IsRemoteSpeaking && blink);
            if (m_VoicePttButton != null)
            SetSelected(m_VoicePttButton, voice.PushToTalk);
            if (m_VoiceAutoButton != null)
            SetSelected(m_VoiceAutoButton, !voice.PushToTalk);
        }

        private async void StartVoiceForCurrentLobby()
        {
            try
            {
                var voice = VoiceChatManager.EnsureInstance();
                voice.ServerUrl = voiceServerUrl;
                await voice.JoinLobbyAsync(m_CurrentLobbyId, m_CurrentLobbyCode);
            }
            catch (Exception ex) { Debug.LogWarning($"[Lobby3D] Voice connection unavailable: {ex.Message}"); }
        }

        private async void OnChatSendClicked()
        {
            if (m_ChatSending || string.IsNullOrEmpty(m_CurrentLobbyId) || m_ChatInput == null) return;
            var content = m_ChatInput.text?.Trim();
            if (string.IsNullOrEmpty(content)) return;
            var sender = !string.IsNullOrEmpty(ApiClient.DisplayName) ? ApiClient.DisplayName : ApiClient.Username;
            var message = new ChatMessage { id = Guid.NewGuid().ToString("N"), sender = sender ?? "Player", content = content };
            m_OwnChatMessages.Add(message);
            while (m_OwnChatMessages.Count > 5) m_OwnChatMessages.RemoveAt(0);
            m_ChatSending = true;
            if (m_ChatSendButton != null) m_ChatSendButton.interactable = false;
            try
            {
                var options = new UpdatePlayerOptions {
                    Data = new Dictionary<string, PlayerDataObject> {
                        { "lobbyChat", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, JsonUtility.ToJson(new ChatFeed { messages = m_OwnChatMessages.ToArray() })) }
                    }
                };
                await LobbyService.Instance.UpdatePlayerAsync(m_CurrentLobbyId, AuthenticationService.Instance.PlayerId, options);
                m_ChatInput.text = "";
                AddChatMessage(message);
            }
            catch (Exception ex)
            {
                m_OwnChatMessages.Remove(message);
                ShowToast($"Không gửi được tin nhắn: {ex.Message}");
            }
            finally
            {
                m_ChatSending = false;
                if (m_ChatSendButton != null) m_ChatSendButton.interactable = true;
                m_ChatInput.ActivateInputField();
            }
        }

        private void ReceiveChatMessages(Lobby lobby)
        {
            if (lobby?.Players == null) return;
            foreach (var player in lobby.Players)
            {
                if (player.Data == null || !player.Data.TryGetValue("lobbyChat", out var data) || string.IsNullOrEmpty(data.Value)) continue;
                try
                {
                    var feed = JsonUtility.FromJson<ChatFeed>(data.Value);
                    if (feed?.messages == null) continue;
                    foreach (var message in feed.messages) AddChatMessage(message);
                }
                catch (Exception ex) { Debug.LogWarning($"[Lobby3D] Invalid chat message: {ex.Message}"); }
            }
        }

        private void AddChatMessage(ChatMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.id) || !m_SeenChatMessages.Add(message.id) || m_ChatScroll?.content == null) return;
            var row = new GameObject("ChatMessage", typeof(RectTransform), typeof(TextMeshProUGUI));
            row.transform.SetParent(m_ChatScroll.content, false);
            var label = row.GetComponent<TextMeshProUGUI>();
            label.text = $"{message.sender}: {message.content}";
            label.fontSize = 12;
            label.color = new Color(0.92f, 0.92f, 0.94f);
            label.textWrappingMode = TextWrappingModes.Normal;
            row.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            while (m_ChatScroll.content.childCount > 60)
                Destroy(m_ChatScroll.content.GetChild(0).gameObject);
            Canvas.ForceUpdateCanvases();
            m_ChatScroll.verticalNormalizedPosition = 0;
        }

        private static void ClearScroll(ScrollRect scroll)
        {
            if (scroll?.content == null) return;
            foreach (Transform child in scroll.content) Destroy(child.gameObject);
        }
        private static void SetSelected(Button button, bool selected)
        {
            if (button == null) return;
            if (button.targetGraphic != null)
                button.targetGraphic.color = selected ? new Color(0.9f, 0.66f, 0.16f) : new Color(0.16f, 0.17f, 0.21f, 0.94f);
            SetButtonTextColor(button, selected ? new Color(0.08f, 0.06f, 0.02f) : Color.white);
        }
        private static void SetButtonText(Button button, string text)
        {
            var label = button?.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = text;
        }
        private static void SetButtonColor(Button button, Color color)
        {
            if (button?.targetGraphic != null) button.targetGraphic.color = color;
        }
        private static void SetButtonTextColor(Button button, Color color)
        {
            var label = button?.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.color = color;
        }

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
                try { await Task.Delay((int)(Mathf.Clamp(pollInterval, 1f, 3f) * 1000), ct); }
                catch (TaskCanceledException) { break; }
                if (ct.IsCancellationRequested) break;

                try
                {
                    var lobby = await LobbyService.Instance.GetLobbyAsync(m_CurrentLobbyId);
                    UpdateLobbyUI(MapToFakeResponse(lobby));
                    ReceiveChatMessages(lobby);

                    if (lobby.Data.ContainsKey("status") && lobby.Data["status"].Value == "InGame" && !m_IsHost)
                    {
                        StopPolling();
                        LobbySessionContext.StopHeartbeat(); // Dừng keep-alive trước khi vào game scene
                        ShowToast("Đang Load Game Scene...");

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
            if (m_RemoteProfileCard != null) m_RemoteProfileCard.SetActive(hasPlayer2);
            if (hasPlayer2 && m_RemotePlayerLabel != null)
            {
                string self = AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : null;
                string remoteId = lobby.player1Id == self ? lobby.player2Id : lobby.player1Id;
                m_RemotePlayerLabel.text = "Người chơi " + (string.IsNullOrEmpty(remoteId) ? "2" : remoteId.Substring(0, Mathf.Min(6, remoteId.Length)));
            }
            if (m_RoomStatusLabel != null)
            {
                if (hasPlayer2)
                {
                    m_RoomStatusLabel.text = "Sẵn sàng!";
                    m_RoomStatusLabel.color = new Color(0.3f, 1f, 0.5f);
                }
                else
                {
                    m_RoomStatusLabel.text = "Đang chờ người chơi thứ 2... (1/2)";
                    m_RoomStatusLabel.color = new Color(1f, 0.8f, 0.4f);
                }
            }

            // Role display
            if (m_PlayerRoleLabel != null)
            {
                string myId = AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : null;
                if (!string.IsNullOrEmpty(myId))
                {
                    if (myId == lobby.dadUserId)
                        m_PlayerRoleLabel.text = "Vai trò: Ba";
                    else if (myId == lobby.childUserId)
                        m_PlayerRoleLabel.text = "Vai trò: Con";
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
                m_PlayButton.interactable = canPlay;
                if (canPlay)
                {
                    SetButtonColor(m_PlayButton, new Color(0.90f, 0.66f, 0.16f)); // Vàng hoàng kim
                    SetButtonTextColor(m_PlayButton, new Color(0.08f, 0.06f, 0.02f)); // Chữ tối màu
                }
                else
                {
                    SetButtonColor(m_PlayButton, new Color(1f, 1f, 1f, 0.1f));
                    SetButtonTextColor(m_PlayButton, new Color(1f, 1f, 1f, 0.3f));
                }
            }

            // Swap: Chỉ Host mới được phép Swap (vì chỉ Host mới có quyền update Unity Lobby)
            if (m_SwapRoleButton != null)
                m_SwapRoleButton.interactable = m_IsHost;
        }

        private void ShowToast(string message)
        {
            if (m_ToastLabel != null)
            {
                m_ToastLabel.text = message;
                if (m_ToastPanel != null) m_ToastPanel.SetActive(!string.IsNullOrEmpty(message));
            }
        }

        #endregion
    }
}
