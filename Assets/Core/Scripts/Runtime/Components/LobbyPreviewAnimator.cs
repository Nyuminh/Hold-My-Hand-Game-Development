using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Đảm bảo nhân vật preview trong scene Lobby luôn chơi animation Idle mượt mà:
    /// - Tắt CoreAnimator (vì không có Network session / CoreMovement trong Lobby)
    /// - Thiết lập các tham số Animator: Speed = 0, MotionSpeed = 1, Grounded = true
    /// - Đặt CullingMode = AlwaysAnimate để không bị pause khi góc camera thay đổi
    /// </summary>
    public class LobbyPreviewAnimator : MonoBehaviour
    {
        private Animator m_Animator;
        private static readonly int s_SpeedHash = Animator.StringToHash("Speed");
        private static readonly int s_MotionSpeedHash = Animator.StringToHash("MotionSpeed");
        private static readonly int s_GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int s_JumpHash = Animator.StringToHash("Jump");
        private static readonly int s_FreeFallHash = Animator.StringToHash("FreeFall");

        private void Awake()
        {
            // Tắt CoreAnimator trên preview model vì không có CoreMovement / Network session trong Lobby
            var coreAnim = GetComponent<CoreAnimator>();
            if (coreAnim != null)
            {
                coreAnim.enabled = false;
            }

            m_Animator = GetComponent<Animator>();
            if (m_Animator != null)
            {
                m_Animator.enabled = true;
                m_Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        private void OnEnable()
        {
            ApplyIdleParameters();
        }

        private void Start()
        {
            ApplyIdleParameters();
        }

        private void Update()
        {
            ApplyIdleParameters();
        }

        private void ApplyIdleParameters()
        {
            if (m_Animator == null || !m_Animator.isActiveAndEnabled) return;

            m_Animator.SetFloat(s_SpeedHash, 0f);
            m_Animator.SetFloat(s_MotionSpeedHash, 1f);
            m_Animator.SetBool(s_GroundedHash, true);
            m_Animator.SetBool(s_JumpHash, false);
            m_Animator.SetBool(s_FreeFallHash, false);
        }
    }
}
