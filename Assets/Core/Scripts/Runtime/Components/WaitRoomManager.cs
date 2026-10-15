using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using System.Collections;
using Unity.Services.Multiplayer;
using Cursor = UnityEngine.Cursor;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Hiển thị giao diện phòng chờ khi người chơi mới vào game.
    /// Khóa di chuyển, góc nhìn, hiện chuột. Khi đủ 2 người thì mở khóa di chuyển, ẩn chuột, vào game.
    /// Hỗ trợ cả Server lẫn Client: Client sử dụng số lượng PlayerObject đã spawn và tin nhắn mạng đồng bộ từ Host.
    /// </summary>
    public class WaitRoomManager : MonoBehaviour
    {
        [Tooltip("Giao diện phòng chờ WaitRoomScreen.uxml")]
        [SerializeField] private UIDocument waitRoomUI;

        private const string MsgStartGame = "HMH_WaitRoom_StartGame";

        private Label m_SessionNameLabel;
        private Label m_PlayerNameLabel;
        private Label m_StatusLabel;

        private bool m_IsWaiting = false;
        private bool m_IsStarting = false;
        private bool m_HandlerRegistered = false;
        private CoreMovement m_LocalMovement;
        private MonoBehaviour m_LocalInput;

        private void OnEnable()
        {
            if (waitRoomUI != null)
            {
                var root = waitRoomUI.rootVisualElement;
                m_SessionNameLabel = root.Q<Label>("session-name-label");
                m_PlayerNameLabel = root.Q<Label>("player-name-label");
                m_StatusLabel = root.Q<Label>("status-label");

                waitRoomUI.rootVisualElement.style.display = DisplayStyle.None;
            }

            RegisterNetworkHandler();
        }

        private void Start()
        {
            RegisterNetworkHandler();
        }

        private void OnDisable()
        {
            UnregisterNetworkHandler();
        }

        private void OnDestroy()
        {
            UnregisterNetworkHandler();
        }

        private void RegisterNetworkHandler()
        {
            if (m_HandlerRegistered || NetworkManager.Singleton == null) return;
            var msgMgr = NetworkManager.Singleton.CustomMessagingManager;
            if (msgMgr != null)
            {
                msgMgr.RegisterNamedMessageHandler(MsgStartGame, OnStartGameMessageReceived);
                m_HandlerRegistered = true;
                Debug.Log("[WaitRoomManager] Đã đăng ký nhận Network Message StartGame thành công.");
            }
        }

        private void UnregisterNetworkHandler()
        {
            if (!m_HandlerRegistered || NetworkManager.Singleton == null) return;
            var msgMgr = NetworkManager.Singleton.CustomMessagingManager;
            if (msgMgr != null)
            {
                msgMgr.UnregisterNamedMessageHandler(MsgStartGame);
            }
            m_HandlerRegistered = false;
        }

        private void OnStartGameMessageReceived(ulong senderClientId, FastBufferReader reader)
        {
            Debug.Log("[WaitRoomManager] Client nhận được thông báo StartGame từ Host!");
            if (m_IsWaiting && !m_IsStarting)
            {
                StartCoroutine(AutoStartGameRoutine());
            }
        }

        private int GetPlayerCount()
        {
            if (NetworkManager.Singleton == null) return 0;

            // Trên Server/Host: ConnectedClients.Count là chuẩn nhất
            if (NetworkManager.Singleton.IsServer)
            {
                return NetworkManager.Singleton.ConnectedClients.Count;
            }

            // Trên Client (non-host): Netcode không đồng bộ ConnectedClients sang client!
            // Kiểm tra số lượng PlayerObject đã spawn thông qua SpawnManager
            int count = 0;
            if (NetworkManager.Singleton.SpawnManager != null)
            {
                foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
                {
                    if (netObj != null && netObj.IsPlayerObject)
                    {
                        count++;
                    }
                }
            }

            // Fallback: Tìm các nhân vật CoreMovement đã xuất hiện trong Scene
            if (count < 2)
            {
                var movements = FindObjectsByType<CoreMovement>(FindObjectsSortMode.None);
                if (movements != null && movements.Length > count)
                {
                    count = movements.Length;
                }
            }

            return count;
        }

        private void Update()
        {
            if (!m_IsWaiting || m_IsStarting) return;

            if (!m_HandlerRegistered)
            {
                RegisterNetworkHandler();
            }

            // Đảm bảo nhân vật luôn đứng đợi và bị khóa di chuyển trong lúc chờ
            EnsurePlayerLocked();

            if (NetworkManager.Singleton == null) return;

            // Kiểm tra số lượng người chơi kết nối (hoạt động đúng trên cả Host lẫn Client)
            int playerCount = GetPlayerCount();
            if (playerCount >= 2)
            {
                StartCoroutine(AutoStartGameRoutine());
            }
            else
            {
                if (m_StatusLabel != null)
                {
                    m_StatusLabel.text = $"Đang đợi người chơi thứ 2... ({Mathf.Max(1, playerCount)}/2)";
                    m_StatusLabel.style.color = new StyleColor(new Color(1f, 0.8f, 0.4f));
                }
            }
        }

        private void EnsurePlayerLocked()
        {
            if (m_LocalMovement == null || m_LocalMovement.enabled)
            {
                LockPlayer(true);
            }
        }

        /// <summary>
        /// Được gọi từ GameManager khi người chơi vừa spawn ra.
        /// </summary>
        public void EnterWaitRoom(string sessionName = "Game Session")
        {
            m_IsWaiting = true;
            m_IsStarting = false;
            
            RegisterNetworkHandler();

            if (waitRoomUI != null)
                waitRoomUI.rootVisualElement.style.display = DisplayStyle.Flex;

            // Lấy thông tin
            string playerName = ApiClient.IsLoggedIn ? ApiClient.DisplayName ?? ApiClient.Username : "Player";

            if (m_PlayerNameLabel != null) m_PlayerNameLabel.text = playerName;
            if (m_SessionNameLabel != null) m_SessionNameLabel.text = sessionName;

            // Khóa di chuyển nhân vật ngay khi vào phòng chờ
            LockPlayer(true);

            // Nếu là người vào sau và phòng đã đủ 2 người rồi thì tự động vào game ngay
            if (GetPlayerCount() >= 2)
            {
                StartCoroutine(AutoStartGameRoutine());
            }
        }

        private IEnumerator AutoStartGameRoutine()
        {
            m_IsStarting = true;

            // Nếu là Server/Host, broadcast tin nhắn StartGame cho tất cả Client
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                var msgMgr = NetworkManager.Singleton.CustomMessagingManager;
                if (msgMgr != null)
                {
                    using (var writer = new FastBufferWriter(sizeof(byte), Unity.Collections.Allocator.Temp))
                    {
                        writer.WriteValueSafe((byte)1);
                        msgMgr.SendNamedMessageToAll(MsgStartGame, writer);
                        Debug.Log("[WaitRoomManager] Host đã phát sóng thông điệp StartGame cho tất cả client.");
                    }
                }
            }

            if (m_StatusLabel != null)
            {
                m_StatusLabel.text = "Đã đủ người chơi! Bắt đầu... (2/2)";
                m_StatusLabel.style.color = new StyleColor(new Color(0.2f, 1f, 0.4f)); // Xanh lá
            }

            try { UISoundManager.PlayConfirm(); } catch { }

            // Chờ 0.5s để người chơi thấy thông báo 2/2 chuyển sang xanh lá
            yield return new WaitForSeconds(0.5f);

            m_IsWaiting = false;
            m_IsStarting = false;

            if (waitRoomUI != null)
                waitRoomUI.rootVisualElement.style.display = DisplayStyle.None;

            // Mở khóa nhân vật & ẩn chuột, bắt đầu di chuyển
            LockPlayer(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LockPlayer(bool isLocked)
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null) return;
            var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (playerObj == null) return;

            // Tìm CoreMovement để vô hiệu hóa
            if (m_LocalMovement == null)
                m_LocalMovement = playerObj.GetComponent<CoreMovement>();
            
            if (m_LocalMovement != null)
            {
                m_LocalMovement.enabled = !isLocked;
                if (isLocked) m_LocalMovement.ResetMovementForces();
            }

            // Tìm Input Component (Ví dụ UnityEngine.InputSystem.PlayerInput)
            if (m_LocalInput == null)
            {
                m_LocalInput = playerObj.GetComponent("PlayerInput") as MonoBehaviour; 
            }

            if (m_LocalInput != null)
                m_LocalInput.enabled = !isLocked;
        }
    }
}
