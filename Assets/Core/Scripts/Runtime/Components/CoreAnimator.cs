using UnityEngine;
using Unity.Netcode.Components;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Controls the player's Animator component based on the state of the <see cref="CoreMovement"/> controller.
    /// This component is responsible for setting locomotion parameters (speed, grounded, jump, etc.)
    /// and handling Animation Events to trigger sound effects like footsteps and landing sounds.
    /// It inherits from <see cref="NetworkAnimator"/> to automatically synchronize animation states across the network.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CoreAnimator : NetworkAnimator
    {
        #region Fields & Properties

        [Header("Component Dependencies")]
        [Tooltip("Reference to the CoreMovement component to get movement state information.")]
        [SerializeField] private CoreMovement coreMovement;

        [Header("Sound Effects")]
        [Tooltip("Sound definition for footstep sounds.")]
        [SerializeField] private SoundDef soundDefFootstep;

        private readonly int m_AnimIDSpeed = Animator.StringToHash("Speed");
        private readonly int m_AnimIDGrounded = Animator.StringToHash("Grounded");
        private readonly int m_AnimIDJump = Animator.StringToHash("Jump");
        private readonly int m_AnimIDFreeFall = Animator.StringToHash("FreeFall");
        private readonly int m_AnimIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        private readonly int m_AnimIDIsSitting = Animator.StringToHash("IsSitting");
        private readonly int m_AnimIDIsCrouching = Animator.StringToHash("IsCrouching");

        private CoopHoldHandAbility m_HoldHandAbility;
        private CharacterController m_CharacterController;
        private Vector3 m_LastRootPosition;
        private float m_SmoothedSpeed = 0f;

        #endregion

        #region Unity & Network Lifecycle

        protected override bool OnIsServerAuthoritative()
        {
            // Cho phép Client quyền tự quyết định Animation của chính mình (Owner-authoritative)
            return false;
        }

        protected override void Awake()
        {
            base.Awake();
            if (coreMovement == null)
            {
                coreMovement = GetComponentInParent<CoreMovement>() ?? GetComponent<CoreMovement>();
            }

            m_CharacterController = GetComponentInParent<CharacterController>() ?? GetComponent<CharacterController>();
            m_HoldHandAbility = GetComponentInParent<CoopHoldHandAbility>() ?? GetComponent<CoopHoldHandAbility>();

            Transform rootT = coreMovement != null ? coreMovement.transform : transform.root;
            m_LastRootPosition = rootT.position;

            if (Animator != null)
            {
                Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            var smrs = rootT.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                smr.updateWhenOffscreen = true;
            }

            if (coreMovement == null)
            {
                Debug.LogError("[Core Animator] needs a CoreMovement component.");
            }

            if (soundDefFootstep == null)
            {
                Debug.LogError("[Core Animator] Footstep SoundDef is not assigned.");
            }
        }

        private void Start()
        {
            if (m_HoldHandAbility == null)
            {
                m_HoldHandAbility = GetComponentInParent<CoopHoldHandAbility>() ?? GetComponent<CoopHoldHandAbility>();
            }
            if (m_CharacterController == null)
            {
                m_CharacterController = GetComponentInParent<CharacterController>() ?? GetComponent<CharacterController>();
            }
            Transform rootT = coreMovement != null ? coreMovement.transform : transform.root;
            m_LastRootPosition = rootT.position;
        }

        private void Update()
        {
            UpdateLocomotionParameters();
        }

        #endregion

        #region Animation Events

        public void OnFootstepWalk(AnimationEvent animationEvent)
        {
            // >=0.5 - We want to be sure that if both walk and run weights are equal, that we only trigger one SFX.
            if (animationEvent.animatorClipInfo.weight >= 0.5f)
            {
                OnFootstep(animationEvent, 0, 0.75f, 0);
            }
        }

        public void OnFootstepRun(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                OnFootstep(animationEvent, 500, 1, 2000);
            }
        }

        /// <summary>
        /// This method is called by an AnimationEvent defined in the walk/run animation clips.
        /// It plays a random footstep sound.
        /// </summary>
        public void OnFootstep(AnimationEvent animationEvent, float walkRunPitchCents, float walkRunVolumeScale, float filterCutoffOffset)
        {
            var director = CoreDirector.GetInstance();
            if (soundDefFootstep == null || director == null || !director.IsSoundSystemReady) return;

            var overrideData = new SoundEmitter.SoundDefOverrideData
            {
                BasePitchInCents = walkRunPitchCents,
                VolumeScale = walkRunVolumeScale,
                BaseLowPassCutoff = filterCutoffOffset
            };

            CoreDirector.RequestAudio(soundDefFootstep)
                .AttachedTo(transform)
                .WithOverrides(overrideData)
                .AsReserved(SoundEmitter.ReservedInfo.ReservedEmitterAndAudioSources)
                .Play();
        }

        /// <summary>
        /// This method is called by an AnimationEvent defined in the landing animation clip.
        /// It plays the landing sound effect.
        /// </summary>
        public void OnLand(AnimationEvent animationEvent)
        {
            var director = CoreDirector.GetInstance();
            if (soundDefFootstep == null || director == null || !director.IsSoundSystemReady) return;

            CoreDirector.RequestAudio(soundDefFootstep)
                .AttachedTo(transform)
                .AsReserved(SoundEmitter.ReservedInfo.ReservedEmitterAndAudioSources)
                .Play();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Đồng bộ thông số di chuyển sang Animator:
        /// Kết hợp tốc độ phím bấm, tốc độ dịch chuyển vật lý thực tế, và trạng thái kéo/chạy khi nắm tay (Co-op Hold Hand).
        /// </summary>
        private void UpdateLocomotionParameters()
        {
            if (Animator == null) return;

            Transform rootT = coreMovement != null ? coreMovement.transform : transform.root;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            // 1. Vận tốc dịch chuyển thực tế trong không gian thế giới (tránh bị trượt tượng sáp khi bị kéo hoặc di chuyển mạng)
            Vector3 flatDelta = Vector3.ProjectOnPlane(rootT.position - m_LastRootPosition, Vector3.up);
            float displacementSpeed = flatDelta.magnitude / dt;
            m_LastRootPosition = rootT.position;

            // 2. Vận tốc từ hệ thống di chuyển người chơi (WASD)
            float motorSpeed = coreMovement != null ? coreMovement.CurrentSpeed : 0f;

            // 3. Vận tốc và trạng thái khi đang Nắm Tay (Co-op Hold Hand)
            float holdHandSpeed = 0f;
            bool isPartnerSprinting = false;
            bool isPartnerCrouching = false;
            if (m_HoldHandAbility != null && m_HoldHandAbility.IsHoldingHands)
            {
                holdHandSpeed = m_HoldHandAbility.PartnerMoveSpeed;
                isPartnerSprinting = m_HoldHandAbility.IsPartnerSprinting;
                isPartnerCrouching = m_HoldHandAbility.IsPartnerCrouching;
            }

            // 4. Tổng hợp tốc độ di chuyển mục tiêu
            float targetSpeed = Mathf.Max(motorSpeed, displacementSpeed, holdHandSpeed);

            // Khi đang nắm tay: nếu người bạn đang chạy sprint hoặc đang di chuyển nhanh -> kích hoạt sải bước chạy tương ứng
            if (m_HoldHandAbility != null && m_HoldHandAbility.IsHoldingHands)
            {
                if (isPartnerSprinting && (targetSpeed > 0.3f || displacementSpeed > 0.3f || holdHandSpeed > 0.3f))
                {
                    targetSpeed = Mathf.Max(targetSpeed, 6.0f);
                }
                else if (targetSpeed > 0.3f || displacementSpeed > 0.3f || holdHandSpeed > 0.3f)
                {
                    targetSpeed = Mathf.Max(targetSpeed, 3.5f);
                }
            }

            if (targetSpeed < 0.15f)
            {
                targetSpeed = 0f;
            }

            // Làm mượt tốc độ chuyển đổi giữa các trạng thái để chân sải bước tự nhiên
            m_SmoothedSpeed = Mathf.Lerp(m_SmoothedSpeed, targetSpeed, dt * 16f);
            if (m_SmoothedSpeed < 0.05f) m_SmoothedSpeed = 0f;

            // 5. Trạng thái chạm đất & rơi tự do:
            // Đảm bảo không bị kẹt ở trạng thái InAir khi là Dummy bot hoặc khi bị kéo trên mặt đất
            bool isGrounded = true;
            if (coreMovement != null && coreMovement.IsGrounded)
            {
                isGrounded = true;
            }
            else if (m_CharacterController != null && m_CharacterController.isGrounded)
            {
                isGrounded = true;
            }
            else if (m_HoldHandAbility != null && m_HoldHandAbility.IsHoldingHands)
            {
                isGrounded = true;
            }
            else if (coreMovement != null)
            {
                isGrounded = coreMovement.IsGrounded;
            }

            float verticalVelocity = coreMovement != null ? coreMovement.VerticalVelocity : 0f;

            Animator.SetBool(m_AnimIDGrounded, isGrounded);
            Animator.SetBool(m_AnimIDJump, !isGrounded && verticalVelocity > 0.1f);
            Animator.SetBool(m_AnimIDFreeFall, !isGrounded && verticalVelocity <= -0.1f);

            // 6. Gửi Speed & MotionSpeed sang Animator Blend Tree
            Animator.SetFloat(m_AnimIDSpeed, m_SmoothedSpeed);
            Animator.SetFloat(m_AnimIDMotionSpeed, 1.0f);

            // 7. Trạng thái khom người / ngồi
            bool isCrouched = (coreMovement != null && coreMovement.IsCrouching) || isPartnerCrouching;
            Animator.SetBool(m_AnimIDIsCrouching, isCrouched);
            Animator.SetBool(m_AnimIDIsSitting, isCrouched);
        }

        public void TurnInPlaceStart()
        {
            Animator.applyRootMotion = true;
        }

        public void TurnInPlaceEnd()
        {
            Animator.applyRootMotion = false;
        }

        #endregion
    }
}
