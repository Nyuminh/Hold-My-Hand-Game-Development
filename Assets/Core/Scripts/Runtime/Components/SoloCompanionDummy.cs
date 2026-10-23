using UnityEngine;
using Unity.Netcode;
using Unity.Cinemachine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Component gắn vào Bạn Đồng Hành (Companion Dummy Bot) trong chế độ Tutorial:
    /// - Đảm bảo Dummy KHÔNG BAO GIỜ nhận input từ bàn phím / chuột của người chơi.
    /// - Gỡ bỏ toàn bộ Event Listeners của CorePlayerManager (onMoveInput, onLookInput...).
    /// - Tắt & Hủy các controller điều khiển góc nhìn và giao diện người dùng để không cướp Camera của người chơi.
    /// - Cho phép nhận lực ném và đứng yên chờ đợi tương tác co-op từ người chơi.
    /// </summary>
    public class SoloCompanionDummy : MonoBehaviour
    {
        private CoreMovement m_Motor;
        private CorePlayerManager m_PlayerManager;
        private CharacterController m_CharacterController;

        private void Awake()
        {
            m_Motor = GetComponent<CoreMovement>();
            m_PlayerManager = GetComponent<CorePlayerManager>();
            m_CharacterController = GetComponent<CharacterController>();
            
            DisconnectInputs();
        }

        private void Start()
        {
            DisconnectInputs();
        }

        private void Update()
        {
            // Luôn đảm bảo Dummy đứng yên, không chạy theo WASD của người chơi
            if (m_Motor != null)
            {
                m_Motor.SetMoveInput(Vector2.zero);
                m_Motor.SetSprintState(false);
                // Giữ IsMovementEnabled = true để CharacterController và trọng lực / lực ném hoạt động
                m_Motor.IsMovementEnabled = true;
            }
        }

        /// <summary>
        /// Ngắt toàn bộ input điều khiển và camera để Dummy không chiếm camera của người chơi và không di chuyển theo.
        /// </summary>
        public void DisconnectInputs()
        {
            // 1. Tắt CorePlayerManager và gỡ toàn bộ event listeners
            if (m_PlayerManager != null)
            {
                m_PlayerManager.UnregisterEventListeners();
                m_PlayerManager.SetMovementInputEnabled(false);
                m_PlayerManager.enabled = false;
            }

            // 2. Tắt CoreInputHandler
            if (TryGetComponent<CoreInputHandler>(out var inputHandler))
            {
                inputHandler.enabled = false;
            }

            // 3. Hủy bỏ hoàn toàn Camera Controller, AudioListener, CoreHUD trên Dummy để không cướp Camera
            if (TryGetComponent<CoreCameraController>(out var cam))
            {
                cam.enabled = false;
                Destroy(cam);
            }
            if (TryGetComponent<AudioListener>(out var listener))
            {
                Destroy(listener);
            }
            if (TryGetComponent<CoreHUD>(out var hud))
            {
                hud.enabled = false;
                Destroy(hud);
            }

            // Dọn dẹp bất kỳ Cinemachine camera con nào nếu có
            var cinemachineCams = GetComponentsInChildren<CinemachineCamera>(true);
            foreach (var cc in cinemachineCams)
            {
                Destroy(cc.gameObject);
            }

            // 4. Đảm bảo CoreMovement không có move input dư thừa
            if (m_Motor != null)
            {
                m_Motor.SetMoveInput(Vector2.zero);
                m_Motor.SetSprintState(false);
                m_Motor.IsMovementEnabled = true;
            }
        }
    }
}
