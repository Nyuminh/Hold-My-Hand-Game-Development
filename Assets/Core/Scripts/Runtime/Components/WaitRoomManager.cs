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
    /// Khóa di chuyển, góc nhìn, hiện chuột. Khi đủ 2 người thì nút PLAY sáng lên.
    /// Khi bấm PLAY, mở khóa di chuyển, ẩn chuột, vào game.
    /// </summary>
    public class WaitRoomManager : MonoBehaviour
    {
        [Tooltip("Giao diện phòng chờ WaitRoomScreen.uxml")]
        [SerializeField] private UIDocument waitRoomUI;

        private Label m_SessionNameLabel;
        private Label m_PlayerNameLabel;
        private Label m_StatusLabel;

        private bool m_IsWaiting = false;
        private bool m_IsStarting = false;
        private CoreMovement m_LocalMovement;
        private MonoBehaviour m_LocalInput; // Ví dụ: PlayerInput hoặc CoreInput

        private void OnEnable()
        {
            if (waitRoomUI == null) return;
            var root = waitRoomUI.rootVisualElement;
            m_SessionNameLabel = root.Q<Label>("session-name-label");
            m_PlayerNameLabel = root.Q<Label>("player-name-label");
            m_StatusLabel = root.Q<Label>("status-label");

            waitRoomUI.rootVisualElement.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            if (!m_IsWaiting || m_IsStarting) return;

            // Đảm bảo nhân vật luôn đứng đợi và bị khóa di chuyển trong lúc chờ
            EnsurePlayerLocked();

            if (NetworkManager.Singleton == null) return;

            // Kiểm tra số lượng người chơi kết nối
            int playerCount = NetworkManager.Singleton.ConnectedClients.Count;
            if (playerCount >= 2)
            {
                // Đủ 2 người chơi → Tự động bắt đầu vào game!
                StartCoroutine(AutoStartGameRoutine());
            }
            else
            {
                if (m_StatusLabel != null)
                {
                    m_StatusLabel.text = "Đang đợi người chơi thứ 2... (1/2)";
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
            
            if (waitRoomUI != null)
                waitRoomUI.rootVisualElement.style.display = DisplayStyle.Flex;

            // Lấy thông tin
            string playerName = ApiClient.IsLoggedIn ? ApiClient.DisplayName ?? ApiClient.Username : "Player";

            if (m_PlayerNameLabel != null) m_PlayerNameLabel.text = playerName;
            if (m_SessionNameLabel != null) m_SessionNameLabel.text = sessionName;

            // Khóa di chuyển nhân vật ngay khi vào phòng chờ
            LockPlayer(true);

            // Nếu là người vào sau và phòng đã đủ 2 người rồi thì tự động vào game ngay
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.Count >= 2)
            {
                StartCoroutine(AutoStartGameRoutine());
            }
        }

        private IEnumerator AutoStartGameRoutine()
        {
            m_IsStarting = true;

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
                // Nếu dự án dùng tên component khác, nó sẽ bị khóa góc nhìn nhờ Cursor.lockState = None
            }

            if (m_LocalInput != null)
                m_LocalInput.enabled = !isLocked;
        }
    }
}
