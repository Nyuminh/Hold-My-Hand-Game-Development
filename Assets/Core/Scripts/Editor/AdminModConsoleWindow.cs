using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Unity Editor Window quản trị và điều khiển CRUD dữ liệu Backend cho Admin và Moderator.
    /// - Quản lý phòng chờ trực tuyến trực tiếp qua Unity Gaming Services (UGS Lobby).
    /// - Quản trị tài khoản, màn chơi, nhiệm vụ trên Backend.
    /// - Tinh chỉnh toàn bộ chỉ số RPG (Health, Stamina) và Di chuyển / Nhảy (CoreMovement Settings).
    /// </summary>
    public class AdminModConsoleWindow : EditorWindow
    {
        private enum Tab
        {
            Accounts,
            LobbiesAndSessions,
            ChaptersAndQuests,
            CharactersAndStats,
            AuditLogs
        }

        private Tab m_CurrentTab = Tab.Accounts;
        private Vector2 m_ScrollPos;
        private string m_StatusMessage = "";
        private MessageType m_StatusType = MessageType.Info;

        // Backend URL config
        private string m_BaseUrl = "https://hold-my-hand-backend.vercel.app/api";

        // Tab: Accounts Data
        private AdminApiClient.AccountDto[] m_Accounts;
        private string m_NewUsername = "";
        private string m_NewEmail = "";
        private string m_NewPassword = "Password@123";
        private string m_NewRole = "Player";
        private string m_BanReason = "Vi phạm quy tắc cộng đồng";

        // Tab: Lobbies & Sessions Data (UGS Lobby & Mongo Sessions)
        private List<Lobby> m_UgsLobbies;
        private AdminApiClient.AdminSessionDto[] m_Sessions;

        // Tab: Chapters & Quests Data
        private ModApiClient.ChapterDto[] m_Chapters;
        private ModApiClient.QuestDto[] m_Quests;
        private string m_NewChapterName = "";
        private string m_NewChapterTheme = "Forest";
        private string m_NewChapterDesc = "";
        private int m_SelectedChapterIndex = -1;

        // Tab: Characters & Stats Data
        private ModApiClient.CharacterDto[] m_Characters;

        // Tab: Audit Logs Data
        private AdminApiClient.AuditLogDto[] m_AuditLogs;

        [MenuItem("Tools/Hold My Hand/Admin & Mod Console", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<AdminModConsoleWindow>("Admin & Mod Console");
            window.minSize = new Vector2(760, 550);
            window.Show();
        }

        private void OnEnable()
        {
            ApiClient.LoadSession();
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(6);

            DrawAuthBar();
            EditorGUILayout.Space(6);

            DrawTabBar();
            EditorGUILayout.Space(6);

            DrawStatus();

            m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);
            switch (m_CurrentTab)
            {
                case Tab.Accounts:
                    DrawAccountsTab();
                    break;
                case Tab.LobbiesAndSessions:
                    DrawLobbiesAndSessionsTab();
                    break;
                case Tab.ChaptersAndQuests:
                    DrawChaptersAndQuestsTab();
                    break;
                case Tab.CharactersAndStats:
                    DrawCharactersAndStatsTab();
                    break;
                case Tab.AuditLogs:
                    DrawAuditLogsTab();
                    break;
            }
            EditorGUILayout.EndScrollView();
        }

        // ─── Header & Config ────────────────────────────────────────

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("HOLD MY HAND — ADMIN & MODERATOR CONSOLE", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            string newUrl = EditorGUILayout.TextField("Backend Base URL:", m_BaseUrl);
            if (newUrl != m_BaseUrl)
            {
                m_BaseUrl = newUrl;
                ApiClient.SetBaseUrl(m_BaseUrl);
            }
            if (GUILayout.Button("Localhost:5181", GUILayout.Width(110)))
            {
                m_BaseUrl = "http://localhost:5181/api";
                ApiClient.SetBaseUrl(m_BaseUrl);
            }
            if (GUILayout.Button("Vercel Cloud", GUILayout.Width(100)))
            {
                m_BaseUrl = "https://hold-my-hand-backend.vercel.app/api";
                ApiClient.SetBaseUrl(m_BaseUrl);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // ─── Authentication Status Bar ──────────────────────────────

        private void DrawAuthBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            if (ApiClient.IsLoggedIn)
            {
                string roleTag = ApiClient.Role switch
                {
                    "Admin" => "[ADMIN]",
                    "Moderator" => "[MODERATOR]",
                    _ => "[PLAYER]"
                };
                EditorGUILayout.LabelField($"Đăng nhập: {ApiClient.Username} ({ApiClient.DisplayName}) {roleTag}", EditorStyles.boldLabel);

                if (GUILayout.Button("Đăng xuất", GUILayout.Width(90)))
                {
                    _ = LogoutAsync();
                }
            }
            else
            {
                EditorGUILayout.LabelField("Chưa đăng nhập — Đăng nhập tài khoản quản trị:", EditorStyles.label);
                if (GUILayout.Button("Login Admin (admin)", GUILayout.Width(160)))
                {
                    _ = QuickLoginAsync("admin", "Admin@123");
                }
                if (GUILayout.Button("Login Mod (moderator)", GUILayout.Width(170)))
                {
                    _ = QuickLoginAsync("moderator", "Mod@123");
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // ─── Navigation Tabs ────────────────────────────────────────

        private void DrawTabBar()
        {
            string[] tabNames = {
                "Tài khoản (Admin)",
                "Phòng & Trận đấu (UGS)",
                "Màn & Nhiệm vụ (Mod)",
                "Nhân vật & Stats (Mod)",
                "Nhật ký Logs (Admin)"
            };
            m_CurrentTab = (Tab)GUILayout.Toolbar((int)m_CurrentTab, tabNames, GUILayout.Height(28));
        }

        private void DrawStatus()
        {
            if (!string.IsNullOrEmpty(m_StatusMessage))
            {
                EditorGUILayout.HelpBox(m_StatusMessage, m_StatusType);
            }
        }

        // ─── Tab 1: Accounts Management ─────────────────────────────

        private void DrawAccountsTab()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Tải danh sách tài khoản (Refresh)", GUILayout.Height(26)))
            {
                _ = FetchAccountsAsync();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // Create New Account Form
            EditorGUILayout.LabelField("Tạo tài khoản mới:", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            m_NewUsername = EditorGUILayout.TextField("Username:", m_NewUsername);
            m_NewEmail = EditorGUILayout.TextField("Email:", m_NewEmail);
            m_NewPassword = EditorGUILayout.PasswordField("Password:", m_NewPassword);
            
            string[] roles = { "Player", "Moderator", "Admin" };
            int selectedRoleIndex = Array.IndexOf(roles, m_NewRole);
            if (selectedRoleIndex < 0) selectedRoleIndex = 0;
            selectedRoleIndex = EditorGUILayout.Popup("Vai trò (Role):", selectedRoleIndex, roles);
            m_NewRole = roles[selectedRoleIndex];

            if (GUILayout.Button("Tạo tài khoản (+ Create)", GUILayout.Height(24)))
            {
                _ = CreateAccountAsync();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // Accounts List
            EditorGUILayout.LabelField($"Danh sách tài khoản ({m_Accounts?.Length ?? 0}):", EditorStyles.boldLabel);
            if (m_Accounts == null || m_Accounts.Length == 0)
            {
                EditorGUILayout.HelpBox("Chưa có dữ liệu tài khoản. Bấm nút Refresh ở trên.", MessageType.None);
                return;
            }

            foreach (var acc in m_Accounts)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                string statusText = acc.isBanned ? $"[BANNED: {acc.banReason}]" : (acc.isActive ? "[Active]" : "[Inactive]");
                EditorGUILayout.LabelField($"{acc.username} ({acc.displayName ?? "No Name"}) | Role: {acc.role} | {statusText}", EditorStyles.boldLabel);

                if (acc.isBanned)
                {
                    if (GUILayout.Button("Unban", GUILayout.Width(70)))
                    {
                        _ = UnbanAccountAsync(acc.id);
                    }
                }
                else
                {
                    if (GUILayout.Button("Ban", GUILayout.Width(60)))
                    {
                        _ = BanAccountAsync(acc.id);
                    }
                }

                if (GUILayout.Button("Xoá", GUILayout.Width(60)))
                {
                    if (EditorUtility.DisplayDialog("Xác nhận xoá", $"Bạn có chắc muốn xoá tài khoản {acc.username}?", "Xoá", "Huỷ"))
                    {
                        _ = DeleteAccountAsync(acc.id);
                    }
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField($"Email: {acc.email} | ID: {acc.id} | Ngày tạo: {acc.createdAt}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
        }

        // ─── Tab 2: Lobbies & Sessions Management (Unity Gaming Services) ──

        private void DrawLobbiesAndSessionsTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("HỆ THỐNG PHÒNG TRỰC TUYẾN (UNITY GAMING SERVICES - UGS LOBBY)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Phòng được lưu trữ và đồng bộ realtime qua dịch vụ đám mây của Unity Gaming Services.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Quét phòng chờ trực tuyến (Unity Lobby Service)", GUILayout.Height(28)))
            {
                _ = FetchUgsLobbiesAsync();
            }
            if (GUILayout.Button("Quét lịch sử ván đấu (MongoDB Sessions)", GUILayout.Height(28)))
            {
                _ = FetchSessionsAsync();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // Active UGS Lobbies
            EditorGUILayout.LabelField($"Phòng chờ đang mở trên Unity Gaming Services ({m_UgsLobbies?.Count ?? 0}):", EditorStyles.boldLabel);
            if (m_UgsLobbies != null && m_UgsLobbies.Count > 0)
            {
                foreach (var lobby in m_UgsLobbies)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();

                    string status = lobby.Data != null && lobby.Data.ContainsKey("status") ? lobby.Data["status"].Value : "Waiting";
                    string dadUser = lobby.Data != null && lobby.Data.ContainsKey("dadUserId") ? lobby.Data["dadUserId"].Value : "N/A";
                    string childUser = lobby.Data != null && lobby.Data.ContainsKey("childUserId") ? lobby.Data["childUserId"].Value : "Trống";
                    string session = lobby.Data != null && lobby.Data.ContainsKey("sessionId") ? lobby.Data["sessionId"].Value : "Chưa tạo";

                    EditorGUILayout.LabelField($"Code: {lobby.LobbyCode} | Tên: {lobby.Name} | Players: {lobby.Players.Count}/{lobby.MaxPlayers} | Trạng thái: {status}", EditorStyles.boldLabel);

                    GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                    if (GUILayout.Button("Force Đóng Phòng", GUILayout.Width(130)))
                    {
                        if (EditorUtility.DisplayDialog("Xác nhận", $"Bạn có chắc muốn đóng phòng {lobby.LobbyCode} trên Unity Services?", "Đóng phòng", "Huỷ"))
                        {
                            _ = ForceCloseUgsLobbyAsync(lobby.Id);
                        }
                    }
                    GUI.backgroundColor = Color.white;

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.LabelField($"Lobby ID: {lobby.Id} | Host ID: {lobby.HostId}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Dad Player: {dadUser} | Child Player: {childUser} | Session ID: {session}", EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Không có phòng nào đang mở trên Unity Services. Bấm 'Quét phòng chờ trực tuyến' để làm mới.", MessageType.None);
            }

            EditorGUILayout.Space(12);

            // Game Sessions in MongoDB
            EditorGUILayout.LabelField($"Lịch sử phiên trận đấu trong MongoDB ({m_Sessions?.Length ?? 0}):", EditorStyles.boldLabel);
            if (m_Sessions != null && m_Sessions.Length > 0)
            {
                foreach (var ses in m_Sessions)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Session: {ses._id} | Lobby: {ses.lobbyCode} | Status: {ses.status}", EditorStyles.boldLabel);

                    if (ses.status == "Active" || ses.status == "InProgress")
                    {
                        if (GUILayout.Button("Force Dừng (Abort)", GUILayout.Width(140)))
                        {
                            _ = AbortSessionAsync(ses._id);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.LabelField($"Bắt đầu: {ses.startedAt} | Kết thúc: {ses.endedAt ?? "Đang diễn ra"}", EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Chưa có phiên trận đấu nào được ghi nhận trong MongoDB.", MessageType.None);
            }
        }

        // ─── Tab 3: Chapters & Quests Management ────────────────────

        private void DrawChaptersAndQuestsTab()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Tải danh sách Chapters (Refresh)", GUILayout.Height(26)))
            {
                _ = FetchChaptersAsync();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // Add Chapter Form
            EditorGUILayout.LabelField("Tạo Chapter mới (Màn chơi):", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            m_NewChapterName = EditorGUILayout.TextField("Tên Chapter:", m_NewChapterName);
            m_NewChapterTheme = EditorGUILayout.TextField("Chủ đề (Theme):", m_NewChapterTheme);
            m_NewChapterDesc = EditorGUILayout.TextField("Mô tả:", m_NewChapterDesc);

            if (GUILayout.Button("Tạo Chapter (+ Create)", GUILayout.Height(24)))
            {
                _ = CreateChapterAsync();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // Chapters List
            EditorGUILayout.LabelField($"Danh sách Chapters ({m_Chapters?.Length ?? 0}):", EditorStyles.boldLabel);
            if (m_Chapters != null)
            {
                for (int i = 0; i < m_Chapters.Length; i++)
                {
                    var ch = m_Chapters[i];
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"[#{ch.orderIndex}] {ch.name} (Theme: {ch.theme})", EditorStyles.boldLabel);

                    if (GUILayout.Button(m_SelectedChapterIndex == i ? "Đóng Quests" : "Xem Quests", GUILayout.Width(100)))
                    {
                        if (m_SelectedChapterIndex == i)
                        {
                            m_SelectedChapterIndex = -1;
                            m_Quests = null;
                        }
                        else
                        {
                            m_SelectedChapterIndex = i;
                            _ = FetchQuestsAsync(ch.id);
                        }
                    }

                    if (GUILayout.Button("Xoá", GUILayout.Width(50)))
                    {
                        if (EditorUtility.DisplayDialog("Xoá Chapter", $"Xoá chapter {ch.name}?", "Xoá", "Huỷ"))
                        {
                            _ = DeleteChapterAsync(ch.id);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.LabelField($"ID: {ch.id} | Mô tả: {ch.description}", EditorStyles.miniLabel);

                    if (m_SelectedChapterIndex == i && m_Quests != null)
                    {
                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField($"--- Quests của {ch.name} ({m_Quests.Length}) ---", EditorStyles.boldLabel);
                        foreach (var q in m_Quests)
                        {
                            EditorGUILayout.BeginVertical(GUI.skin.box);
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField($"• {q.name} [{q.questType}]", EditorStyles.label);
                            if (GUILayout.Button("Xoá Quest", GUILayout.Width(80)))
                            {
                                _ = DeleteQuestAsync(q.id, ch.id);
                            }
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.LabelField($"  Mô tả: {q.description}", EditorStyles.miniLabel);
                            EditorGUILayout.EndVertical();
                        }
                    }

                    EditorGUILayout.EndVertical();
                }
            }
        }

        // ─── Tab 4: Characters & Full Stats Tuning ──────────────────

        private void DrawCharactersAndStatsTab()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Tải danh sách Characters & Stats từ Backend", GUILayout.Height(28)))
            {
                _ = FetchCharactersAsync();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            if (m_Characters == null || m_Characters.Length == 0)
            {
                EditorGUILayout.HelpBox("Chưa có dữ liệu nhân vật. Bấm nút Tải ở trên.", MessageType.None);
                return;
            }

            foreach (var ch in m_Characters)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"NHÂN VẬT: {ch.name.ToUpper()} (ID: {ch.id})", EditorStyles.boldLabel);

                // ── 1. RPG Stats (Health, Stamina) ──
                if (ch.attributes != null && ch.attributes.Length > 0)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("1. Chỉ số sinh tồn (Core Stats - Health & Stamina):", EditorStyles.boldLabel);
                    foreach (var attr in ch.attributes)
                    {
                        EditorGUILayout.BeginVertical(GUI.skin.box);
                        EditorGUILayout.LabelField($"Thuộc tính: {attr.statName}", EditorStyles.boldLabel);
                        EditorGUILayout.BeginHorizontal();
                        attr.startingValue = EditorGUILayout.FloatField("Starting Value:", attr.startingValue);
                        attr.maxValue = EditorGUILayout.FloatField("Max Value:", attr.maxValue);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        attr.regenRate = EditorGUILayout.FloatField("Tốc độ hồi (/s):", attr.regenRate);
                        attr.regenDelay = EditorGUILayout.FloatField("Độ trễ hồi (s):", attr.regenDelay);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                    }
                }

                // ── 2. Movement & Physics Settings (CoreMovement) ──
                if (ch.movement == null) ch.movement = new ModApiClient.MovementSettingsDto();

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("2. Chỉ số di chuyển & Nhảy (CoreMovement Settings):", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(GUI.skin.box);
                
                EditorGUILayout.LabelField("Movement Settings:", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                ch.movement.moveSpeed = EditorGUILayout.FloatField("Move Speed:", ch.movement.moveSpeed);
                ch.movement.sprintSpeed = EditorGUILayout.FloatField("Sprint Speed:", ch.movement.sprintSpeed);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                ch.movement.rotationSmoothTime = EditorGUILayout.FloatField("Rotation Smooth Time:", ch.movement.rotationSmoothTime);
                ch.movement.speedChangeRate = EditorGUILayout.FloatField("Speed Change Rate:", ch.movement.speedChangeRate);
                EditorGUILayout.EndHorizontal();

                ch.movement.forceDecayRate = EditorGUILayout.FloatField("Force Decay Rate:", ch.movement.forceDecayRate);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Gravity and Jump Settings:", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                ch.movement.gravity = EditorGUILayout.FloatField("Gravity:", ch.movement.gravity);
                ch.movement.jumpHeight = EditorGUILayout.FloatField("Jump Height:", ch.movement.jumpHeight);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Ground Check & Slope Handling:", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                ch.movement.groundedOffset = EditorGUILayout.FloatField("Grounded Offset:", ch.movement.groundedOffset);
                ch.movement.groundedRadius = EditorGUILayout.FloatField("Grounded Radius:", ch.movement.groundedRadius);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                ch.movement.slopeForce = EditorGUILayout.FloatField("Slope Force:", ch.movement.slopeForce);
                ch.movement.slopeForceRayLength = EditorGUILayout.FloatField("Slope Force Ray Length:", ch.movement.slopeForceRayLength);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(6);

                // Action buttons
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
                if (GUILayout.Button($"💾 Lưu cấu hình {ch.name} lên Backend", GUILayout.Height(26)))
                {
                    _ = SaveCharacterStatsAsync(ch);
                }

                GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
                if (GUILayout.Button($"⚡ Áp dụng ngay cho nhân vật trong Scene", GUILayout.Height(26)))
                {
                    ApplyMovementToSceneCharacters(ch);
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(6);
            }
        }

        // ─── Tab 5: Audit Logs ──────────────────────────────────────

        private void DrawAuditLogsTab()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Tải Audit Logs (Refresh)", GUILayout.Height(26)))
            {
                _ = FetchAuditLogsAsync();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            if (m_AuditLogs != null && m_AuditLogs.Length > 0)
            {
                foreach (var log in m_AuditLogs)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"[{log.createdAt}] Action: {log.action} | Đối tượng: {log.targetType} (#{log.targetId})", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Thực hiện bởi Admin/Mod ID: {log.adminId}", EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Chưa có logs hoặc chưa tải.", MessageType.None);
            }
        }

        // ─── Async Actions ──────────────────────────────────────────

        private async Task QuickLoginAsync(string username, string password)
        {
            SetStatus($"Đang đăng nhập tài khoản '{username}'...", MessageType.Info);
            try
            {
                var res = await ApiClient.LoginAsync(username, password);
                SetStatus($"Đăng nhập thành công với vai trò: {res.role}", MessageType.Info);
                Repaint();
            }
            catch (Exception ex)
            {
                SetStatus($"Đăng nhập thất bại: {ex.Message}", MessageType.Error);
            }
        }

        private async Task LogoutAsync()
        {
            await ApiClient.LogoutAsync();
            SetStatus("Đã đăng xuất.", MessageType.Info);
            Repaint();
        }

        private async Task FetchAccountsAsync()
        {
            SetStatus("Đang tải danh sách tài khoản...", MessageType.Info);
            try
            {
                m_Accounts = await AdminApiClient.GetAccountsAsync();
                SetStatus($"Đã tải {m_Accounts.Length} tài khoản thành công.", MessageType.Info);
                Repaint();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi tải tài khoản: {ex.Message}", MessageType.Error);
            }
        }

        private async Task CreateAccountAsync()
        {
            if (string.IsNullOrWhiteSpace(m_NewUsername) || string.IsNullOrWhiteSpace(m_NewEmail))
            {
                SetStatus("Vui lòng điền đủ Username và Email!", MessageType.Warning);
                return;
            }

            SetStatus("Đang tạo tài khoản...", MessageType.Info);
            try
            {
                var req = new AdminApiClient.CreateAccountRequest
                {
                    username = m_NewUsername.Trim(),
                    email = m_NewEmail.Trim(),
                    password = m_NewPassword,
                    role = m_NewRole
                };
                await AdminApiClient.CreateAccountAsync(req);
                SetStatus($"Tạo tài khoản '{req.username}' thành công!", MessageType.Info);
                m_NewUsername = "";
                m_NewEmail = "";
                await FetchAccountsAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi tạo tài khoản: {ex.Message}", MessageType.Error);
            }
        }

        private async Task BanAccountAsync(string id)
        {
            try
            {
                await AdminApiClient.BanAccountAsync(id, m_BanReason);
                SetStatus("Đã khoá tài khoản thành công.", MessageType.Info);
                await FetchAccountsAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi khoá tài khoản: {ex.Message}", MessageType.Error);
            }
        }

        private async Task UnbanAccountAsync(string id)
        {
            try
            {
                await AdminApiClient.UnbanAccountAsync(id);
                SetStatus("Đã mở khoá tài khoản.", MessageType.Info);
                await FetchAccountsAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi unban: {ex.Message}", MessageType.Error);
            }
        }

        private async Task DeleteAccountAsync(string id)
        {
            try
            {
                await AdminApiClient.DeleteAccountAsync(id);
                SetStatus("Đã xoá tài khoản.", MessageType.Info);
                await FetchAccountsAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi xoá tài khoản: {ex.Message}", MessageType.Error);
            }
        }

        // ─── UGS Lobby Management ───────────────────────────────────

        private async Task FetchUgsLobbiesAsync()
        {
            SetStatus("Đang kết nối Unity Gaming Services để quét phòng...", MessageType.Info);
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                var options = new QueryLobbiesOptions { Count = 25 };
                var queryResponse = await LobbyService.Instance.QueryLobbiesAsync(options);
                m_UgsLobbies = queryResponse.Results;

                SetStatus($"Đã quét được {m_UgsLobbies.Count} phòng trực tuyến trên Unity Services.", MessageType.Info);
                Repaint();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi quét phòng Unity Services: {ex.Message}", MessageType.Error);
            }
        }

        private async Task ForceCloseUgsLobbyAsync(string lobbyId)
        {
            try
            {
                await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                SetStatus($"Đã đóng phòng ID: {lobbyId} trên Unity Services.", MessageType.Info);
                await FetchUgsLobbiesAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi đóng phòng Unity Services: {ex.Message}", MessageType.Error);
            }
        }

        private async Task FetchSessionsAsync()
        {
            SetStatus("Đang quét lịch sử game sessions từ MongoDB...", MessageType.Info);
            try
            {
                m_Sessions = await AdminApiClient.GetSessionsAsync();
                SetStatus($"Đã quét {m_Sessions.Length} phiên trận từ MongoDB.", MessageType.Info);
                Repaint();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi quét sessions: {ex.Message}", MessageType.Error);
            }
        }

        private async Task AbortSessionAsync(string sessionId)
        {
            try
            {
                await AdminApiClient.AbortSessionAsync(sessionId);
                SetStatus($"Đã huỷ phiên trận {sessionId}.", MessageType.Info);
                await FetchSessionsAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi dừng phiên: {ex.Message}", MessageType.Error);
            }
        }

        private async Task FetchChaptersAsync()
        {
            SetStatus("Đang tải danh sách chapters...", MessageType.Info);
            try
            {
                m_Chapters = await ModApiClient.GetChaptersAsync();
                SetStatus($"Đã tải {m_Chapters.Length} chapters.", MessageType.Info);
                Repaint();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi tải chapters: {ex.Message}", MessageType.Error);
            }
        }

        private async Task CreateChapterAsync()
        {
            if (string.IsNullOrWhiteSpace(m_NewChapterName))
            {
                SetStatus("Vui lòng nhập tên Chapter!", MessageType.Warning);
                return;
            }

            try
            {
                var req = new ModApiClient.CreateChapterRequest
                {
                    name = m_NewChapterName.Trim(),
                    theme = m_NewChapterTheme.Trim(),
                    description = m_NewChapterDesc.Trim(),
                    orderIndex = (m_Chapters?.Length ?? 0) + 1,
                    isActive = true
                };
                await ModApiClient.CreateChapterAsync(req);
                SetStatus($"Tạo Chapter '{req.name}' thành công!", MessageType.Info);
                m_NewChapterName = "";
                m_NewChapterDesc = "";
                await FetchChaptersAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi tạo chapter: {ex.Message}", MessageType.Error);
            }
        }

        private async Task DeleteChapterAsync(string id)
        {
            try
            {
                await ModApiClient.DeleteChapterAsync(id);
                SetStatus("Đã xoá chapter.", MessageType.Info);
                await FetchChaptersAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi xoá chapter: {ex.Message}", MessageType.Error);
            }
        }

        private async Task FetchQuestsAsync(string chapterId)
        {
            try
            {
                m_Quests = await ModApiClient.GetQuestsAsync(chapterId);
                Repaint();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi tải quests: {ex.Message}", MessageType.Error);
            }
        }

        private async Task DeleteQuestAsync(string questId, string chapterId)
        {
            try
            {
                await ModApiClient.DeleteQuestAsync(questId);
                SetStatus("Đã xoá quest.", MessageType.Info);
                await FetchQuestsAsync(chapterId);
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi xoá quest: {ex.Message}", MessageType.Error);
            }
        }

        private async Task FetchCharactersAsync()
        {
            SetStatus("Đang tải danh sách nhân vật & chỉ số...", MessageType.Info);
            try
            {
                m_Characters = await ModApiClient.GetCharactersAsync();
                CharacterStatsService.UpdateLocalCache(m_Characters);
                SetStatus($"Đã tải {m_Characters.Length} nhân vật.", MessageType.Info);
                Repaint();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi tải nhân vật: {ex.Message}", MessageType.Error);
            }
        }

        private async Task SaveCharacterStatsAsync(ModApiClient.CharacterDto character)
        {
            SetStatus($"Đang lưu toàn bộ chỉ số cho '{character.name}'...", MessageType.Info);
            try
            {
                // Cập nhật bộ nhớ cục bộ ngay lập tức để game nhận ngay
                CharacterStatsService.UpdateLocalCache(character);

                var req = new ModApiClient.UpdateCharacterRequest
                {
                    name = character.name,
                    physicsType = character.physicsType,
                    primaryStatName = character.primaryStatName,
                    isActive = character.isActive,
                    attributes = character.attributes,
                    movement = character.movement
                };
                await ModApiClient.UpdateCharacterAsync(character.id, req);
                SetStatus($"Đã lưu chỉ số (RPG & CoreMovement) của '{character.name}' lên Backend thành công!", MessageType.Info);
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi lưu chỉ số: {ex.Message}", MessageType.Error);
            }
        }

        private void ApplyMovementToSceneCharacters(ModApiClient.CharacterDto character)
        {
            if (character == null) return;

            // Cập nhật bộ nhớ runtime cục bộ
            CharacterStatsService.UpdateLocalCache(character);

            var movements = UnityEngine.Object.FindObjectsByType<CoreMovement>();
            if (movements == null || movements.Length == 0)
            {
                SetStatus("Không tìm thấy nhân vật nào có component CoreMovement trong Scene hiện tại!", MessageType.Warning);
                return;
            }

            int count = 0;
            bool isDad = character.id == CharacterStatsService.DAD_ID || (!string.IsNullOrEmpty(character.name) && character.name.ToLower().Contains("dad"));

            foreach (var cm in movements)
            {
                bool targetIsDad = cm.gameObject.name.ToLower().Contains("dad");
                if (targetIsDad != isDad && movements.Length > 1) continue;

                CharacterStatsService.ApplyStatsToCharacter(cm.gameObject, isDad);
                EditorUtility.SetDirty(cm);
                if (cm.TryGetComponent<CoreStatsHandler>(out var stats))
                {
                    EditorUtility.SetDirty(stats);
                }
                count++;
            }

            SetStatus($"Đã áp dụng các chỉ số di chuyển & sinh tồn cho {count} nhân vật trong Scene!", MessageType.Info);
        }

        private async Task FetchAuditLogsAsync()
        {
            SetStatus("Đang tải audit logs...", MessageType.Info);
            try
            {
                m_AuditLogs = await AdminApiClient.GetLogsAsync();
                SetStatus($"Đã tải {m_AuditLogs.Length} logs.", MessageType.Info);
                Repaint();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi tải audit logs: {ex.Message}", MessageType.Error);
            }
        }

        private void SetStatus(string msg, MessageType type)
        {
            m_StatusMessage = msg;
            m_StatusType = type;
            EditorApplication.delayCall += Repaint;
            Repaint();
        }
    }
}
