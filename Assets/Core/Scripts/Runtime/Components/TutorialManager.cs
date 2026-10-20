using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Netcode;
using Cursor = UnityEngine.Cursor;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Quản lý chế độ tập luyện (Tutorial / Practice Mode):
    /// - Tự động khởi chạy Host cục bộ cho 1 người chơi (không cần bạn bè, không cần cloud relay).
    /// - Cho phép tự do chọn hoặc đổi qua lại giữa 🧔 Ba (Dad) và 👶 Con (Child).
    /// - Cung cấp bảng hướng dẫn thao tác phím (WASD, Sprint, Jump, Mouse look) và nút trở về sảnh.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("UI Document")]
        [Tooltip("UIDocument chứa TutorialHUD.uxml")]
        [SerializeField] private UIDocument tutorialHUD;

        [Header("Spawn Reference")]
        [Tooltip("Điểm xuất phát ban đầu")]
        [SerializeField] private Transform spawnPoint;

        private Label m_RoleBadgeLabel;
        private Button m_SwitchRoleButton;
        private Button m_ReturnLobbyButton;

        private bool m_IsSwitching = false;
        private RoleBasedSpawner m_Spawner;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            SetupUI();
            StartCoroutine(InitTutorialRoutine());
        }

        private void Update()
        {
            // Phím tắt T để đổi vai
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                ToggleRole();
            }
#else
            if (Input.GetKeyDown(KeyCode.T))
            {
                ToggleRole();
            }
#endif
        }

        private void SetupUI()
        {
            if (tutorialHUD == null)
            {
                tutorialHUD = GetComponent<UIDocument>();
            }

            if (tutorialHUD == null) return;

            var root = tutorialHUD.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            m_RoleBadgeLabel = root.Q<Label>("role-badge-label");
            m_SwitchRoleButton = root.Q<Button>("switch-role-btn");
            m_ReturnLobbyButton = root.Q<Button>("return-lobby-btn");

            if (m_SwitchRoleButton != null)
                m_SwitchRoleButton.clicked += ToggleRole;

            if (m_ReturnLobbyButton != null)
                m_ReturnLobbyButton.clicked += ReturnToLobby;

            UpdateRoleBadge();
            UISoundManager.BindButtons(root);
        }

        private IEnumerator InitTutorialRoutine()
        {
            yield return null;

            // Xóa cache spawn cũ để tránh conflict
            RoleBasedSpawner.ClearSpawnedClients();

            // Khởi động NetworkManager ở chế độ Host cục bộ (1 người chơi)
            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                    yield return new WaitForSecondsRealtime(0.2f);
                }

                NetworkManager.Singleton.StartHost();
                Debug.Log("[TutorialManager] Khởi động Host cục bộ thành công!");
            }

            // Đợi NetworkManager và Spawner sẵn sàng
            yield return new WaitForSecondsRealtime(0.3f);

            m_Spawner = FindAnyObjectByType<RoleBasedSpawner>();
            if (m_Spawner != null && NetworkManager.Singleton != null)
            {
                // Đảm bảo client 0 có PlayerObject
                var localClient = NetworkManager.Singleton.LocalClient;
                if (localClient == null || localClient.PlayerObject == null)
                {
                    m_Spawner.SpawnForClient(NetworkManager.Singleton.LocalClientId);
                }
            }

            yield return new WaitForSecondsRealtime(0.2f);

            // Mở khóa chuột và di chuyển ngay lập tức (không cần chờ người thứ 2)
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            UpdateRoleBadge();
        }

        /// <summary>
        /// Đổi vai giữa Ba (Dad) và Con (Child) trong khi đang chơi Tutorial.
        /// </summary>
        public void ToggleRole()
        {
            if (m_IsSwitching) return;
            StartCoroutine(SwitchRoleRoutine(!Lobby3DManager.AmIDad));
        }

        private IEnumerator SwitchRoleRoutine(bool newIsDad)
        {
            m_IsSwitching = true;
            UISoundManager.PlaySwapRole();

            if (m_Spawner == null)
            {
                m_Spawner = FindAnyObjectByType<RoleBasedSpawner>();
            }

            if (m_Spawner != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                m_Spawner.RespawnForClient(NetworkManager.Singleton.LocalClientId, newIsDad);
            }
            else
            {
                Lobby3DManager.AmIDad = newIsDad;
            }

            UpdateRoleBadge();

            yield return new WaitForSecondsRealtime(0.3f);
            m_IsSwitching = false;
        }

        public void UpdateRoleBadge()
        {
            bool isDad = Lobby3DManager.AmIDad;

            if (m_RoleBadgeLabel != null)
            {
                m_RoleBadgeLabel.text = isDad
                    ? "Đang điều khiển: 🧔 BA (DAD)"
                    : "Đang điều khiển: 👶 CON (CHILD)";
                m_RoleBadgeLabel.style.color = isDad
                    ? new StyleColor(new Color(0.95f, 0.75f, 0.25f))
                    : new StyleColor(new Color(0.35f, 0.85f, 1f));
            }

            if (m_SwitchRoleButton != null)
            {
                m_SwitchRoleButton.text = isDad
                    ? "🔄  ĐỔI SANG CON (👶) [T]"
                    : "🔄  ĐỔI SANG BA (🧔) [T]";
            }
        }

        /// <summary>
        /// Trở về Sảnh Chờ (Lobby).
        /// </summary>
        public void ReturnToLobby()
        {
            UISoundManager.PlayClick();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            SceneManager.LoadScene("Lobby");
        }
    }
}
