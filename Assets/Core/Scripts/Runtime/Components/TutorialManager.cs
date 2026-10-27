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
        private Button m_TeleportBoatButton;

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
            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
            {
                TeleportToBoat();
            }
#else
            if (Input.GetKeyDown(KeyCode.T))
            {
                ToggleRole();
            }
            if (Input.GetKeyDown(KeyCode.B))
            {
                TeleportToBoat();
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
            m_TeleportBoatButton = root.Q<Button>("teleport-boat-btn");

            if (m_SwitchRoleButton != null)
                m_SwitchRoleButton.clicked += ToggleRole;

            if (m_ReturnLobbyButton != null)
                m_ReturnLobbyButton.clicked += ReturnToLobby;

            if (m_TeleportBoatButton != null)
                m_TeleportBoatButton.clicked += TeleportToBoat;

            UpdateRoleBadge();
            UISoundManager.BindButtons(root);
        }

        private GameObject m_CompanionInstance;

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            }
            CleanupCompanion();
        }

        private IEnumerator InitTutorialRoutine()
        {
            yield return null;

            // Kiểm tra xem có phải đang kết nối Multiplayer từ Lobby qua AutoSessionStarter hay không
            bool isMultiplayerSession = FindAnyObjectByType<AutoSessionStarter>() != null || 
                                         (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.ConnectedClients.Count > 1);

            if (!isMultiplayerSession)
            {
                // Xóa cache spawn cũ để tránh conflict trong chế độ Solo
                RoleBasedSpawner.ClearSpawnedClients();

                // Khởi động NetworkManager ở chế độ Host cục bộ
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
            }
            else
            {
                Debug.Log("[TutorialManager] Phát hiện phiên chơi 2 người từ Lobby. Duy trì kết nối mạng Netcode!");
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
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

            // Mở khóa chuột và di chuyển ngay lập tức
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Nếu chỉ có 1 người chơi (Solo Test): Tự động tạo Bạn Đồng Hành (Companion) để test co-op
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.Count <= 1)
            {
                SpawnOrUpdateCompanion(Lobby3DManager.AmIDad);
            }

            UpdateRoleBadge();
        }

        private void OnClientConnected(ulong clientId)
        {
            // Khi có người chơi thật thứ 2 tham gia qua mạng -> Tự động dọn dẹp Dummy bot
            if (NetworkManager.Singleton != null && clientId != NetworkManager.Singleton.LocalClientId)
            {
                CleanupCompanion();
                Debug.Log($"[TutorialManager] 🎮 Người chơi thứ 2 (Client {clientId}) đã kết nối! Đã dọn dẹp Dummy companion.");
            }
        }

        private void CleanupCompanion()
        {
            if (m_CompanionInstance != null)
            {
                var netObj = m_CompanionInstance.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
                else
                {
                    Destroy(m_CompanionInstance);
                }
                m_CompanionInstance = null;
            }
        }

        private void SpawnOrUpdateCompanion(bool isLocalPlayerDad)
        {
            if (m_Spawner == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            CleanupCompanion();

            // Chỉ spawn companion khi chỉ có 1 client duy nhất
            if (NetworkManager.Singleton.ConnectedClients.Count <= 1)
            {
                bool companionIsDad = !isLocalPlayerDad;
                GameObject companionPrefab = m_Spawner.ResolvePrefab(companionIsDad);
                if (companionPrefab != null)
                {
                    Vector3 localPos = Vector3.zero;
                    Quaternion localRot = Quaternion.identity;

                    if (NetworkManager.Singleton.LocalClient?.PlayerObject != null)
                    {
                        localPos = NetworkManager.Singleton.LocalClient.PlayerObject.transform.position;
                        localRot = NetworkManager.Singleton.LocalClient.PlayerObject.transform.rotation;
                    }
                    else if (spawnPoint != null)
                    {
                        localPos = spawnPoint.position;
                        localRot = spawnPoint.rotation;
                    }

                    Vector3 companionPos = localPos + localRot * (Vector3.right * 1.8f);
                    m_CompanionInstance = Instantiate(companionPrefab, companionPos, localRot);
                    m_CompanionInstance.name = companionIsDad ? "Dad_Companion_Dummy" : "Child_Companion_Dummy";

                    CharacterStatsService.ApplyStatsToCharacter(m_CompanionInstance, companionIsDad);

                    // Xóa hoàn toàn CameraController, AudioListener và CoreHUD trên Dummy TRƯỚC KHI Spawn
                    if (m_CompanionInstance.TryGetComponent<CoreCameraController>(out var dummyCam))
                    {
                        DestroyImmediate(dummyCam);
                    }
                    if (m_CompanionInstance.TryGetComponent<AudioListener>(out var dummyListener))
                    {
                        DestroyImmediate(dummyListener);
                    }
                    if (m_CompanionInstance.TryGetComponent<CoreHUD>(out var dummyHud))
                    {
                        DestroyImmediate(dummyHud);
                    }

                    // Gắn component SoloCompanionDummy để cô lập hoàn toàn input của dummy
                    var dummy = m_CompanionInstance.GetComponent<SoloCompanionDummy>() ?? m_CompanionInstance.AddComponent<SoloCompanionDummy>();
                    dummy.DisconnectInputs();

                    // Đảm bảo có đủ CoopHoldHandAbility và CoopBoostThrowAbility
                    if (m_CompanionInstance.GetComponent<CoopHoldHandAbility>() == null)
                    {
                        m_CompanionInstance.AddComponent<CoopHoldHandAbility>();
                    }
                    if (m_CompanionInstance.GetComponent<CoopBoostThrowAbility>() == null)
                    {
                        m_CompanionInstance.AddComponent<CoopBoostThrowAbility>();
                    }

                    var netObj = m_CompanionInstance.GetComponent<NetworkObject>();
                    if (netObj != null && !netObj.IsSpawned)
                    {
                        netObj.Spawn(true);
                    }

                    // Gọi lại DisconnectInputs sau khi Netcode Spawn để hủy bất kỳ listener nào do OnNetworkSpawn tự động đăng ký
                    dummy.DisconnectInputs();

                    // Đảm bảo Camera Cinemachine luôn tập trung vào người chơi thật (LocalClient.PlayerObject)
                    if (NetworkManager.Singleton.LocalClient?.PlayerObject != null)
                    {
                        var playerCam = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<CoreCameraController>();
                        if (playerCam != null)
                        {
                            playerCam.SwitchCameraMode("FreeLook");
                        }
                    }

                    Debug.Log($"[TutorialManager] 🤖 Đã tạo Bạn Đồng Hành '{(companionIsDad ? "Dad" : "Child")}' đứng yên làm đối tác test Nắm Tay [H] & Kiệu Phóng [F]!");
                }
            }
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

            // Đồng thời đổi bạn đồng hành đứng cạnh sang vai còn lại
            SpawnOrUpdateCompanion(newIsDad);

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

        /// <summary>
        /// Dịch chuyển người chơi và bạn đồng hành đến khu vực Thuyền tập lái.
        /// </summary>
        public void TeleportToBoat()
        {
            var boat = GameObject.Find("Coop_Wooden_Boat");
            if (boat == null) return;

            UISoundManager.PlayClick();

            Vector3 dockPos = new Vector3(2.4f, 4.0f, 116.5f);
            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject?.gameObject;
            if (localPlayer == null)
            {
                var cam = FindAnyObjectByType<CoreCameraController>();
                if (cam != null) localPlayer = cam.gameObject;
            }

            if (localPlayer != null)
            {
                var cc = localPlayer.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                localPlayer.transform.position = dockPos;
                localPlayer.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                if (cc != null) cc.enabled = true;
            }

            if (m_CompanionInstance != null)
            {
                var dummyCC = m_CompanionInstance.GetComponent<CharacterController>();
                if (dummyCC != null) dummyCC.enabled = false;
                m_CompanionInstance.transform.position = dockPos + Vector3.forward * 1.5f;
                m_CompanionInstance.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                if (dummyCC != null) dummyCC.enabled = true;
            }

            Debug.Log("[TutorialManager] ⛵ Đã dịch chuyển đến khu vực Thuyền tập lái!");
        }
    }
}
