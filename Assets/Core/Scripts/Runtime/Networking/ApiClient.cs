using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// HTTP client để gọi HoldMyHand Backend API từ Unity.
    /// Quản lý JWT token và cung cấp các methods tương ứng với API endpoints.
    /// </summary>
    public static class ApiClient
    {
        // TODO: Đổi thành URL thật khi deploy
        private static string BaseUrl = "https://hold-my-hand-backend.vercel.app/api";

        // JWT token nhận từ login response
        private static string _accessToken;
        private static string _refreshToken;

        // User info
        public static string UserId { get; private set; }
        public static string Username { get; private set; }
        public static string DisplayName { get; private set; }
        public static string SessionId { get; private set; }
        public static string Role { get; private set; }
        public static bool IsLoggedIn => !string.IsNullOrEmpty(_accessToken);

        /// <summary>
        /// Cho phép thay đổi base URL lúc runtime (e.g. từ config).
        /// </summary>
        public static void SetBaseUrl(string url)
        {
            BaseUrl = url.TrimEnd('/');
        }

        // ─── Auth ─────────────────────────────────────────────────

        /// <summary>Đăng nhập bằng username/password, lưu JWT token.</summary>
        public static async Task<LoginResponseData> LoginAsync(string username, string password)
        {
            var body = JsonUtility.ToJson(new LoginRequestData { username = username, password = password });
            var json = await PostAsync("/auth/login", body, authenticated: false);
            var response = JsonUtility.FromJson<LoginResponseData>(json);

            _accessToken = response.accessToken;
            _refreshToken = response.refreshToken;
            UserId = response.userId;
            Username = response.username;
            DisplayName = response.displayName;
            SessionId = response.sessionId;
            Role = response.role;

            SaveSession();

            Debug.Log($"[ApiClient] Logged in as {response.username} (userId: {response.userId})");
            return response;
        }

        /// <summary>Đăng xuất — xóa session Redis phía backend.</summary>
        public static async Task LogoutAsync()
        {
            try
            {
                await PostAsync("/auth/logout", "{}", authenticated: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ApiClient] Logout request failed: {ex.Message}");
            }
            finally
            {
                ClearSession();
            }
        }

        /// <summary>Refresh JWT access token bằng refresh token.</summary>
        public static async Task RefreshTokenAsync()
        {
            if (string.IsNullOrEmpty(_refreshToken))
                throw new InvalidOperationException("No refresh token available.");

            var body = JsonUtility.ToJson(new RefreshRequestData { refreshToken = _refreshToken });
            var json = await PostAsync("/auth/refresh", body, authenticated: false);
            var response = JsonUtility.FromJson<TokenResponseData>(json);

            _accessToken = response.accessToken;
            _refreshToken = response.refreshToken;

            SaveSession();

            Debug.Log("[ApiClient] Token refreshed successfully.");
        }

        public static bool LoadSession()
        {
            if (PlayerPrefs.HasKey("HMH_AccessToken"))
            {
                _accessToken = PlayerPrefs.GetString("HMH_AccessToken");
                _refreshToken = PlayerPrefs.GetString("HMH_RefreshToken");
                UserId = PlayerPrefs.GetString("HMH_UserId");
                Username = PlayerPrefs.GetString("HMH_Username");
                DisplayName = PlayerPrefs.GetString("HMH_DisplayName");
                SessionId = PlayerPrefs.GetString("HMH_SessionId");
                Role = PlayerPrefs.GetString("HMH_Role");
                return true;
            }
            return false;
        }

        private static void SaveSession()
        {
            PlayerPrefs.SetString("HMH_AccessToken", _accessToken);
            PlayerPrefs.SetString("HMH_RefreshToken", _refreshToken);
            PlayerPrefs.SetString("HMH_UserId", UserId);
            PlayerPrefs.SetString("HMH_Username", Username);
            PlayerPrefs.SetString("HMH_DisplayName", DisplayName);
            PlayerPrefs.SetString("HMH_SessionId", SessionId);
            PlayerPrefs.SetString("HMH_Role", Role);
            PlayerPrefs.Save();
        }

        private static void ClearSession()
        {
            _accessToken = null;
            _refreshToken = null;
            UserId = null;
            Username = null;
            DisplayName = null;
            SessionId = null;
            Role = null;

            PlayerPrefs.DeleteKey("HMH_AccessToken");
            PlayerPrefs.DeleteKey("HMH_RefreshToken");
            PlayerPrefs.DeleteKey("HMH_UserId");
            PlayerPrefs.DeleteKey("HMH_Username");
            PlayerPrefs.DeleteKey("HMH_DisplayName");
            PlayerPrefs.DeleteKey("HMH_SessionId");
            PlayerPrefs.DeleteKey("HMH_Role");
            PlayerPrefs.Save();
        }

        // ─── Lobby ────────────────────────────────────────────────

        public static async Task<LobbyResponseData> GetCurrentLobbyAsync()
        {
            var json = await GetAsync("/game/lobby/current");
            return JsonUtility.FromJson<LobbyResponseData>(json);
        }


        /// <summary>Tạo lobby mới. Player tạo lobby = Dad.</summary>
        public static async Task<LobbyResponseData> CreateLobbyAsync(string chapterId = null)
        {
            var body = JsonUtility.ToJson(new CreateLobbyRequestData { chapterId = chapterId });
            var json = await PostAsync("/game/lobby", body);
            return JsonUtility.FromJson<LobbyResponseData>(json);
        }

        /// <summary>Join lobby bằng code. Player join = Child.</summary>
        public static async Task<LobbyResponseData> JoinLobbyAsync(string lobbyCode)
        {
            var body = JsonUtility.ToJson(new JoinLobbyRequestData { lobbyCode = lobbyCode });
            var json = await PostAsync("/game/lobby/join", body);
            return JsonUtility.FromJson<LobbyResponseData>(json);
        }

        /// <summary>Lấy trạng thái lobby hiện tại (dùng cho polling).</summary>
        public static async Task<LobbyResponseData> GetLobbyAsync(string lobbyCode)
        {
            var json = await GetAsync($"/game/lobby/{lobbyCode}");
            return JsonUtility.FromJson<LobbyResponseData>(json);
        }

        /// <summary>Swap Dad ↔ Child roles.</summary>
        public static async Task<LobbyResponseData> SwapRolesAsync(string lobbyCode)
        {
            var json = await PostAsync($"/game/lobby/{lobbyCode}/swap", "{}");
            return JsonUtility.FromJson<LobbyResponseData>(json);
        }

        /// <summary>Ready — lock roles, tạo GameSession trong MongoDB, trả RelayJoinCode.</summary>
        public static async Task<GameStartResponseData> ReadyAsync(string lobbyCode)
        {
            var json = await PostAsync($"/game/lobby/{lobbyCode}/ready", "{}");
            return JsonUtility.FromJson<GameStartResponseData>(json);
        }

        /// <summary>Rời lobby. Nếu game đang chạy sẽ Abandon session.</summary>
        public static async Task LeaveLobbyAsync(string lobbyCode)
        {
            await DeleteAsync($"/game/lobby/{lobbyCode}");
        }

        // ─── HTTP Helpers ──────────────────────────────────────────

        private static async Task<string> PostAsync(string endpoint, string jsonBody, bool authenticated = true)
        {
            var url = BaseUrl + endpoint;
            using var request = new UnityWebRequest(url, "POST");
            var bodyBytes = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            if (authenticated && !string.IsNullOrEmpty(_accessToken))
                request.SetRequestHeader("Authorization", $"Bearer {_accessToken}");

            await SendRequestAsync(request);
            return request.downloadHandler.text;
        }

        private static async Task<string> GetAsync(string endpoint)
        {
            var url = BaseUrl + endpoint;
            using var request = UnityWebRequest.Get(url);
            request.downloadHandler = new DownloadHandlerBuffer();

            if (!string.IsNullOrEmpty(_accessToken))
                request.SetRequestHeader("Authorization", $"Bearer {_accessToken}");

            await SendRequestAsync(request);
            return request.downloadHandler.text;
        }

        private static async Task DeleteAsync(string endpoint)
        {
            var url = BaseUrl + endpoint;
            using var request = UnityWebRequest.Delete(url);
            request.downloadHandler = new DownloadHandlerBuffer();

            if (!string.IsNullOrEmpty(_accessToken))
                request.SetRequestHeader("Authorization", $"Bearer {_accessToken}");

            await SendRequestAsync(request);
        }

        private static Task SendRequestAsync(UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<bool>();
            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    tcs.SetResult(true);
                }
                else
                {
                    var errorMsg = $"API Error [{request.responseCode}] {request.method} {request.url}: {request.error}";
                    var responseBody = request.downloadHandler?.text;
                    if (!string.IsNullOrEmpty(responseBody))
                        errorMsg += $"\nResponse: {responseBody}";

                    Debug.LogError($"[ApiClient] {errorMsg}");
                    tcs.SetException(new ApiException(request.responseCode, request.error, responseBody));
                }
            };
            return tcs.Task;
        }

        // ─── JSON Data Classes ─────────────────────────────────────
        // Phải dùng [Serializable] class thay vì record vì JsonUtility không hỗ trợ record.

        [Serializable]
        public class LoginRequestData
        {
            public string username;
            public string password;
        }

        [Serializable]
        public class LoginResponseData
        {
            public string accessToken;
            public string refreshToken;
            public string sessionId;
            public string userId;
            public string username;
            public string role;
            public string displayName;
        }

        [Serializable]
        public class RefreshRequestData
        {
            public string refreshToken;
        }

        [Serializable]
        public class TokenResponseData
        {
            public string accessToken;
            public string refreshToken;
        }

        [Serializable]
        public class CreateLobbyRequestData
        {
            public string chapterId;
        }

        [Serializable]
        public class JoinLobbyRequestData
        {
            public string lobbyCode;
        }

        [Serializable]
        public class LobbyResponseData
        {
            public string lobbyCode;
            public string player1Id;
            public string player2Id;
            public string dadUserId;
            public string childUserId;
            public string status;        // "Waiting", "Ready", "InGame", "Finished"
            public string chapterId;
            public string createdAt;
        }

        [Serializable]
        public class GameStartResponseData
        {
            public string sessionId;
            public string lobbyCode;
            public string dadProfileId;
            public string childProfileId;
            public string relayJoinCode;
        }
    }

    /// <summary>
    /// Custom exception chứa thông tin lỗi từ backend API.
    /// </summary>
    public class ApiException : Exception
    {
        public long StatusCode { get; }
        public string ResponseBody { get; }

        public ApiException(long statusCode, string error, string responseBody)
            : base($"[{statusCode}] {error}")
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }
}
