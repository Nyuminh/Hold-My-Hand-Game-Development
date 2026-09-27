using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Xử lý UI đăng nhập. Gọi backend API POST /api/auth/login.
    /// Sau khi login thành công → load LobbyScene.
    /// </summary>
    public class LoginManager : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("UIDocument chứa LoginScreen.uxml")]
        [SerializeField] private UIDocument loginUI;

        [Header("Scene")]
        [Tooltip("Tên scene lobby sẽ load sau khi đăng nhập thành công")]
        [SerializeField] private string lobbySceneName = "[BB] Core MultiplayerSession";

        // UI Elements - Login Form
        private VisualElement m_LoginForm;
        private TextField m_UsernameField;
        private TextField m_PasswordField;
        private Button m_LoginButton;

        // UI Elements - Logged In State
        private VisualElement m_LoggedInState;
        private Label m_WelcomeLabel;
        private Button m_PlayButton;
        private Button m_LogoutButton;

        private Label m_StatusLabel;

        private void OnEnable()
        {
            if (loginUI == null)
            {
                Debug.LogError("[LoginManager] loginUI is not assigned.", this);
                return;
            }

            var root = loginUI.rootVisualElement;
            
            m_LoginForm = root.Q<VisualElement>("login-form");
            m_UsernameField = root.Q<TextField>("username-input");
            m_PasswordField = root.Q<TextField>("password-input");
            m_LoginButton = root.Q<Button>("login-button");
            
            m_LoggedInState = root.Q<VisualElement>("logged-in-state");
            m_WelcomeLabel = root.Q<Label>("welcome-label");
            m_PlayButton = root.Q<Button>("play-button");
            m_LogoutButton = root.Q<Button>("logout-button");
            
            m_StatusLabel = root.Q<Label>("status-label");

            if (m_LoginButton != null) m_LoginButton.clicked += OnLoginClicked;
            if (m_PlayButton != null) m_PlayButton.clicked += OnPlayClicked;
            if (m_LogoutButton != null) m_LogoutButton.clicked += OnLogoutClicked;

            // Tự động load token từ PlayerPrefs
            if (ApiClient.LoadSession())
            {
                ShowLoggedInState();
                SetStatus("");
            }
            else
            {
                ShowLoginForm();
            }
        }

        private void OnDisable()
        {
            if (m_LoginButton != null) m_LoginButton.clicked -= OnLoginClicked;
            if (m_PlayButton != null) m_PlayButton.clicked -= OnPlayClicked;
            if (m_LogoutButton != null) m_LogoutButton.clicked -= OnLogoutClicked;
        }

        private void ShowLoginForm()
        {
            if (m_LoginForm != null) m_LoginForm.style.display = DisplayStyle.Flex;
            if (m_LoggedInState != null) m_LoggedInState.style.display = DisplayStyle.None;
        }

        private void ShowLoggedInState()
        {
            if (m_LoginForm != null) m_LoginForm.style.display = DisplayStyle.None;
            if (m_LoggedInState != null) m_LoggedInState.style.display = DisplayStyle.Flex;
            
            if (m_WelcomeLabel != null)
            {
                string name = !string.IsNullOrEmpty(ApiClient.DisplayName) ? ApiClient.DisplayName : ApiClient.Username;
                m_WelcomeLabel.text = $"Xin chào, {name}!";
            }
        }

        private void OnPlayClicked()
        {
            SetStatus("Đang tải Lobby...");
            SceneManager.LoadScene(lobbySceneName);
        }

        private void OnLogoutClicked()
        {
            // Optional: You could call ApiClient.LogoutAsync() here if the backend supports token revocation
            // For now, we'll just clear the local preferences.
            PlayerPrefs.DeleteKey("HMH_AccessToken");
            PlayerPrefs.DeleteKey("HMH_RefreshToken");
            PlayerPrefs.DeleteKey("HMH_UserId");
            PlayerPrefs.DeleteKey("HMH_Username");
            PlayerPrefs.DeleteKey("HMH_DisplayName");
            PlayerPrefs.DeleteKey("HMH_SessionId");
            PlayerPrefs.DeleteKey("HMH_Role");
            PlayerPrefs.Save();

            ShowLoginForm();
            SetStatus("Đã đăng xuất.");
        }

        private async void OnLoginClicked()
        {
            string username = m_UsernameField?.value?.Trim();
            string password = m_PasswordField?.value;

            if (string.IsNullOrWhiteSpace(username))
            {
                SetStatus("Vui lòng nhập username!");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                SetStatus("Vui lòng nhập password!");
                return;
            }

            m_LoginButton.SetEnabled(false);
            SetStatus("Đang đăng nhập...");

            try
            {
                var result = await ApiClient.LoginAsync(username, password);

                string displayText = !string.IsNullOrEmpty(result.displayName)
                    ? result.displayName
                    : result.username;

                SetStatus("");

                // Chuyển sang UI Play thay vì load scene ngay
                ShowLoggedInState();
            }
            catch (ApiException apiEx)
            {
                // Hiển thị lỗi thân thiện hơn dựa trên status code
                string errorMsg = apiEx.StatusCode switch
                {
                    401 => "Sai username hoặc password!",
                    403 => "Tài khoản đã bị khóa.",
                    _ => $"Lỗi: {apiEx.Message}"
                };
                SetStatus(errorMsg);
                m_LoginButton.SetEnabled(true);
                Debug.LogError($"[LoginManager] API error: {apiEx}");
            }
            catch (System.Exception ex)
            {
                SetStatus($"Không thể kết nối server!");
                m_LoginButton.SetEnabled(true);
                Debug.LogError($"[LoginManager] {ex}");
            }
        }

        private void SetStatus(string message)
        {
            if (m_StatusLabel != null)
                m_StatusLabel.text = message;
        }
    }
}
