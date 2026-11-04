using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Blocks.Gameplay.Core
{
    /// <summary>Login form backed by POST /api/auth/login.</summary>
    public class LoginManager : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject loginForm;
        [SerializeField] private GameObject loggedInState;
        [SerializeField] private TMP_InputField usernameField;
        [SerializeField] private TMP_InputField passwordField;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button playButton;
        [SerializeField] private Button logoutButton;
        [SerializeField] private TextMeshProUGUI welcomeLabel;
        [SerializeField] private TextMeshProUGUI statusLabel;
        [SerializeField] private TextMeshProUGUI versionLabel;
        [Header("Scene")]
        [SerializeField] private string lobbySceneName = "Lobby";

        private void OnEnable()
        {
            if (loginButton == null || usernameField == null || passwordField == null)
            {
                Debug.LogError("[LoginManager] Login UI references are missing.", this);
                return;
            }
            passwordField.contentType = TMP_InputField.ContentType.Password;
            if (versionLabel != null) versionLabel.text = "v1.0 — Hold My Hand";
            loginButton.onClick.AddListener(OnLoginClicked);
            if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
            if (logoutButton != null) logoutButton.onClick.AddListener(OnLogoutClicked);
            ApiClient.OnSessionExpired += OnSessionExpired;
            if (ApiClient.LoadSession())
            {
                ShowLoggedInState();
                SetStatus("");
                _ = CharacterStatsService.FetchCharacterStatsAsync();
            }
            else ShowLoginForm();
        }

        private void OnDisable()
        {
            if (loginButton != null) loginButton.onClick.RemoveListener(OnLoginClicked);
            if (playButton != null) playButton.onClick.RemoveListener(OnPlayClicked);
            if (logoutButton != null) logoutButton.onClick.RemoveListener(OnLogoutClicked);
            ApiClient.OnSessionExpired -= OnSessionExpired;
        }

        private void OnSessionExpired()
        {
            ShowLoginForm();
            SetStatus("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại!");
        }
        private void ShowLoginForm()
        {
            if (loginForm != null) loginForm.SetActive(true);
            if (loggedInState != null) loggedInState.SetActive(false);
            if (loginButton != null) loginButton.interactable = true;
        }
        private void ShowLoggedInState()
        {
            if (loginForm != null) loginForm.SetActive(false);
            if (loggedInState != null) loggedInState.SetActive(true);
            if (welcomeLabel != null)
            {
                string name = !string.IsNullOrEmpty(ApiClient.DisplayName) ? ApiClient.DisplayName : ApiClient.Username;
                welcomeLabel.text = $"Xin chào, {name}!";
            }
        }
        private async void OnPlayClicked()
        {
            UISoundManager.PlayConfirm();
            SetStatus("Đang tải dữ liệu và vào Lobby...");
            if (playButton != null) playButton.interactable = false;
            try
            {
                await CharacterStatsService.EnsureLoadedAsync();
                SceneManager.LoadScene(lobbySceneName);
            }
            catch (System.Exception ex)
            {
                SetStatus("Không thể tải dữ liệu nhân vật.");
                if (playButton != null) playButton.interactable = true;
                Debug.LogError($"[LoginManager] {ex}");
            }
        }
        private void OnLogoutClicked()
        {
            UISoundManager.PlayCancel();
            ApiClient.ClearSession();
            ShowLoginForm();
            SetStatus("Đã đăng xuất.");
        }
        private async void OnLoginClicked()
        {
            string username = usernameField.text?.Trim();
            string password = passwordField.text;
            if (string.IsNullOrWhiteSpace(username))
            {
                UISoundManager.PlayError();
                SetStatus("Vui lòng nhập username!");
                return;
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                UISoundManager.PlayError();
                SetStatus("Vui lòng nhập password!");
                return;
            }
            loginButton.interactable = false;
            SetStatus("Đang đăng nhập...");
            try
            {
                await ApiClient.LoginAsync(username, password);
                UISoundManager.PlaySuccess();
                SetStatus("");
                ShowLoggedInState();
                _ = CharacterStatsService.FetchCharacterStatsAsync();
            }
            catch (ApiException apiEx)
            {
                UISoundManager.PlayError();
                SetStatus(apiEx.StatusCode switch
                {
                    401 => "Sai username hoặc password!",
                    403 => "Tài khoản đã bị khóa.",
                    _ => $"Lỗi: {apiEx.Message}"
                });
                Debug.LogError($"[LoginManager] API error: {apiEx}");
            }
            catch (System.Exception ex)
            {
                UISoundManager.PlayError();
                SetStatus("Không thể kết nối server!");
                Debug.LogError($"[LoginManager] {ex}");
            }
            finally { if (loginButton != null) loginButton.interactable = true; }
        }
        private void SetStatus(string message)
        {
            if (statusLabel != null) statusLabel.text = message;
        }
    }
}
