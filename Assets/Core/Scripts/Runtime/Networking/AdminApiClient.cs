using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// API Client dành riêng cho các tính năng quản trị Admin của Hold My Hand.
    /// Tái sử dụng phiên đăng nhập và token từ ApiClient.
    /// </summary>
    public static class AdminApiClient
    {
        // ─── Accounts CRUD ──────────────────────────────────────────

        /// <summary>Lấy danh sách tất cả tài khoản trong hệ thống.</summary>
        public static async Task<AccountDto[]> GetAccountsAsync()
        {
            var json = await ApiClient.GetAsync("/admin/accounts");
            return JsonHelper.FromJsonArray<AccountDto>(json);
        }

        /// <summary>Lấy thông tin chi tiết một tài khoản theo ID.</summary>
        public static async Task<AccountDto> GetAccountByIdAsync(string id)
        {
            var json = await ApiClient.GetAsync($"/admin/accounts/{id}");
            return JsonUtility.FromJson<AccountDto>(json);
        }

        /// <summary>Tạo tài khoản mới (Admin / Moderator / Player).</summary>
        public static async Task<AccountDto> CreateAccountAsync(CreateAccountRequest req)
        {
            var body = JsonUtility.ToJson(req);
            var json = await ApiClient.PostAsync("/admin/accounts", body);
            return JsonUtility.FromJson<AccountDto>(json);
        }

        /// <summary>Cập nhật thông tin tài khoản (email, displayName, avatarUrl, isActive).</summary>
        public static async Task<AccountDto> UpdateAccountAsync(string id, UpdateAccountRequest req)
        {
            var body = JsonUtility.ToJson(req);
            var json = await ApiClient.PutAsync($"/admin/accounts/{id}", body);
            return JsonUtility.FromJson<AccountDto>(json);
        }

        /// <summary>Xoá tài khoản vĩnh viễn.</summary>
        public static async Task DeleteAccountAsync(string id)
        {
            await ApiClient.DeleteAsync($"/admin/accounts/{id}");
        }

        /// <summary>Khoá tài khoản (Ban) và buộc ngắt kết nối phiên hiện tại.</summary>
        public static async Task BanAccountAsync(string id, string reason)
        {
            var body = JsonUtility.ToJson(new BanAccountRequest { reason = reason });
            await ApiClient.PostAsync($"/admin/accounts/{id}/ban", body);
        }

        /// <summary>Mở khoá tài khoản (Unban).</summary>
        public static async Task UnbanAccountAsync(string id)
        {
            await ApiClient.PostAsync($"/admin/accounts/{id}/unban", "{}");
        }

        // ─── Audit Logs ─────────────────────────────────────────────

        /// <summary>Lấy danh sách 500 hành động quản trị gần nhất.</summary>
        public static async Task<AuditLogDto[]> GetLogsAsync()
        {
            var json = await ApiClient.GetAsync("/admin/logs");
            return JsonHelper.FromJsonArray<AuditLogDto>(json);
        }

        // ─── Lobbies Management ─────────────────────────────────────

        /// <summary>Lấy danh sách toàn bộ phòng chờ đang mở trong Redis.</summary>
        public static async Task<AdminLobbyDto[]> GetLobbiesAsync()
        {
            var json = await ApiClient.GetAsync("/admin/lobbies");
            return JsonHelper.FromJsonArray<AdminLobbyDto>(json);
        }

        /// <summary>Cưỡng chế đóng/xoá phòng chờ.</summary>
        public static async Task ForceCloseLobbyAsync(string lobbyCode)
        {
            await ApiClient.DeleteAsync($"/admin/lobbies/{lobbyCode}");
        }

        // ─── Game Sessions Management ───────────────────────────────

        /// <summary>Lấy danh sách các trận đấu trong MongoDB (lọc theo status nếu cần).</summary>
        public static async Task<AdminSessionDto[]> GetSessionsAsync(string status = null)
        {
            var endpoint = string.IsNullOrEmpty(status) ? "/admin/sessions" : $"/admin/sessions?status={status}";
            var json = await ApiClient.GetAsync(endpoint);
            return JsonHelper.FromJsonArray<AdminSessionDto>(json);
        }

        /// <summary>Cưỡng chế dừng phiên trận đấu đang diễn ra.</summary>
        public static async Task AbortSessionAsync(string sessionId)
        {
            await ApiClient.PostAsync($"/admin/sessions/{sessionId}/abort", "{}");
        }

        // ─── DTOs & Models ──────────────────────────────────────────

        [Serializable]
        public class AccountDto
        {
            public string id;
            public string username;
            public string email;
            public string role;
            public bool isActive;
            public bool isBanned;
            public string banReason;
            public string displayName;
            public string avatarUrl;
            public string createdAt;
            public string lastLoginAt;
        }

        [Serializable]
        public class CreateAccountRequest
        {
            public string username;
            public string email;
            public string password;
            public string role; // "Admin", "Moderator", "Player"
        }

        [Serializable]
        public class UpdateAccountRequest
        {
            public string email;
            public string displayName;
            public string avatarUrl;
            public bool isActive = true;
        }

        [Serializable]
        public class BanAccountRequest
        {
            public string reason;
        }

        [Serializable]
        public class AuditLogDto
        {
            public string id;
            public string adminId;
            public string targetType;
            public string targetId;
            public string action;
            public string createdAt;
        }

        [Serializable]
        public class AdminLobbyDto
        {
            public string lobbyCode;
            public string player1Id;
            public string player2Id;
            public string dadUserId;
            public string childUserId;
            public string status;
            public string chapterId;
            public string createdAt;
        }

        [Serializable]
        public class AdminSessionDto
        {
            public string _id;
            public string lobbyCode;
            public string chapterId;
            public string status;
            public string startedAt;
            public string endedAt;
        }
    }
}
