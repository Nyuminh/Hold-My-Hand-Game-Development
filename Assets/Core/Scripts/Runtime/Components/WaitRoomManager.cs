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
        private Button m_PlayButton;

        private bool m_IsWaiting = false;
        private CoreMovement m_LocalMovement;
        private MonoBehaviour m_LocalInput; // Ví dụ: PlayerInput hoặc CoreInput

        private void OnEnable()
        {
            if (waitRoomUI == null) return;
            var root = waitRoomUI.rootVisualElement;
            m_SessionNameLabel = root.Q<Label>("session-name-label");
            m_PlayerNameLabel = root.Q<Label>("player-name-label");
            m_StatusLabel = root.Q<Label>("status-label");
            m_PlayButton = root.Q<Button>("play-button");

            if (m_PlayButton != null)
            {
                m_PlayButton.clicked += OnPlayClicked;
                m_PlayButton.SetEnabled(false);
            }

            waitRoomUI.rootVisualElement.style.display = DisplayStyle.None;
        }

        private void OnDisable()
        {
            if (m_PlayButton != null)
                m_PlayButton.clicked -= OnPlayClicked;
        }

        private void Update()
        {
            if (!m_IsWaiting || NetworkManager.Singleton == null) return;

            // Kiểm tra số lượng người chơi
            int playerCount = NetworkManager.Singleton.ConnectedClients.Count;
            if (playerCount >= 2)
            {
                if (m_StatusLabel != null)
                {
                    m_StatusLabel.text = "Đã đủ người chơi! Bấm PLAY để bắt đầu (2/2)";
                    m_StatusLabel.style.color = new StyleColor(new Color(0.2f, 1f, 0.4f)); // Xanh lá
                }
                
                if (m_PlayButton != null && !m_PlayButton.enabledSelf)
                {
                    m_PlayButton.SetEnabled(true);
                    m_PlayButton.style.backgroundColor = new StyleColor(new Color(0.9f, 0.6f, 0.1f)); // Vàng cam PUBG
                    m_PlayButton.style.color = new StyleColor(Color.white);
                }
            }
            else
            {
                if (m_StatusLabel != null)
                {
                    m_StatusLabel.text = "Đang đợi người chơi thứ 2... (1/2)";
                    m_StatusLabel.style.color = new StyleColor(new Color(1f, 0.8f, 0.4f));
                }

                if (m_PlayButton != null && m_PlayButton.enabledSelf)
                {
                    m_PlayButton.SetEnabled(false);
                    m_PlayButton.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.1f));
                    m_PlayButton.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.3f));
                }
            }

            // Đảm bảo luôn hiện chuột khi đang đợi
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>
        /// Được gọi từ GameManager khi người chơi vừa spawn ra.
        /// </summary>
        public void EnterWaitRoom(string sessionName = "Game Session")
        {
            m_IsWaiting = true;
            
            if (waitRoomUI != null)
                waitRoomUI.rootVisualElement.style.display = DisplayStyle.Flex;

            // Lấy thông tin
            string playerName = ApiClient.IsLoggedIn ? ApiClient.DisplayName ?? ApiClient.Username : "Player";

            if (m_PlayerNameLabel != null) m_PlayerNameLabel.text = playerName;
            if (m_SessionNameLabel != null) m_SessionNameLabel.text = sessionName;

            // Khóa nhân vật
            LockPlayer(true);
        }

        private void OnPlayClicked()
        {
            if (!m_IsWaiting) return;
            m_IsWaiting = false;

            if (waitRoomUI != null)
                waitRoomUI.rootVisualElement.style.display = DisplayStyle.None;

            // Mở khóa nhân vật & ẩn chuột
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
