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
        private Animator m_Animator;
        private CoopHoldHandAbility m_HoldAbility;
        private Vector3 m_LastPos;

        private void Awake()
        {
            m_Motor = GetComponent<CoreMovement>();
            m_PlayerManager = GetComponent<CorePlayerManager>();
            m_CharacterController = GetComponent<CharacterController>();
            m_Animator = GetComponentInChildren<Animator>();
            m_HoldAbility = GetComponent<CoopHoldHandAbility>();
            m_LastPos = transform.position;
            
            DisconnectInputs();
        }

        private void Start()
        {
            DisconnectInputs();
            m_LastPos = transform.position;
        }

        private void Update()
        {
            // Luôn đảm bảo Dummy không nhận phím WASD của người chơi
            if (m_Motor != null)
            {
                m_Motor.SetMoveInput(Vector2.zero);
                m_Motor.SetSprintState(false);
                // Giữ IsMovementEnabled = true để CharacterController và trọng lực / lực ném hoạt động
                m_Motor.IsMovementEnabled = true;
            }

            // Đồng bộ Animation khi KHÔNG ở trong trạng thái nắm tay (ví dụ: rơi tự do, bị ném, đáp đất)
            // Khi đang nắm tay, CoopHoldHandAbility sẽ trực tiếp điều khiển Animator đồng nhịp với Ba
            if (m_Animator != null && (m_HoldAbility == null || !m_HoldAbility.IsHoldingHands))
            {
                bool isGrounded = m_CharacterController != null && m_CharacterController.isGrounded;
                m_Animator.SetBool("Grounded", isGrounded);

                float dt = Time.deltaTime;
                if (dt > 0.001f)
                {
                    Vector3 displacement = transform.position - m_LastPos;
                    displacement.y = 0;
                    float realSpeed = displacement.magnitude / dt;

                    float curSpeed = m_Animator.GetFloat("Speed");
                    float targetSpeed = realSpeed > 0.15f ? realSpeed : 0f;
                    m_Animator.SetFloat("Speed", Mathf.Lerp(curSpeed, targetSpeed, dt * 10f));
                    m_Animator.SetFloat("MotionSpeed", 1.0f);
                }
            }
            m_LastPos = transform.position;
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
