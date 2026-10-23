using System;
using UnityEngine;
using Unity.Netcode;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Cơ chế Nắm Tay (Hold My Hand) - Biểu tượng cốt lõi của trò chơi:
    /// - Cho phép 🧔 Ba (Dad) và 👶 Con (Child) nắm chặt tay nhau khi ở khoảng cách gần.
    /// - Khi nắm tay:
    ///   + Tốc độ di chuyển của Con tự động tăng lên bằng tốc độ của Ba (kéo con chạy cùng).
    ///   + Ba dùng thể lực dìu dắt, giúp Con không bị kiệt sức (Child stamina drain = 0).
    ///   + Giúp cả hai cùng vượt qua các luồng gió cản trở hoặc dòng nước xiết.
    ///   + Tự động tách tay khi khoảng cách vượt quá giới hạn an toàn.
    /// </summary>
    [RequireComponent(typeof(CoreMovement))]
    public class CoopHoldHandAbility : NetworkBehaviour, IMovementAbility
    {
        #region IMovementAbility Implementation

        public int Priority => 15; // Ưu tiên trên WalkAbility (0) và JumpAbility (10)
        public float StaminaCost => 1.0f; // Ba tiêu tốn một lượng nhỏ thể lực duy trì

        #endregion

        #region Serialized Fields

        [Header("Khoảng cách tương tác")]
        [Tooltip("Khoảng cách tối đa để có thể bắt đầu nắm tay (mét)")]
        [SerializeField] private float interactDistance = 2.5f;

        [Tooltip("Khoảng cách tối đa trước khi tự động tuột tay (mét)")]
        [SerializeField] private float breakDistance = 3.5f;

        [Tooltip("Khoảng cách lý tưởng khi đi cạnh nhau")]
        [SerializeField] private float idealDistance = 1.0f;

        [Tooltip("Độ mượt khi kéo theo")]
        [SerializeField] private float followSharpness = 8.0f;

        [Header("Phím tắt")]
        [Tooltip("Phím mặc định để nắm tay")]
        [SerializeField] private KeyCode holdHandKey = KeyCode.H;

        #endregion

        #region Network Variables

        // Đồng bộ trạng thái đang nắm tay
        private readonly NetworkVariable<bool> m_IsHoldingHands = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        #endregion

        #region Private Fields

        private CoreMovement m_Motor;
        private PlayerRoleVisuals m_RoleVisuals;
        private CoreStatsHandler m_StatsHandler;
        private CoopHoldHandAbility m_PartnerHandAbility;
        private Transform m_PartnerTransform;
        private bool m_IsPartnerNearby = false;

        #endregion

        #region Properties

        /// <summary>
        /// Cho biết nhân vật có đang trong trạng thái nắm tay hay không.
        /// </summary>
        public bool IsHoldingHands => m_IsHoldingHands.Value;

        /// <summary>
        /// Kiểm tra xem nhân vật này là Ba hay Con.
        /// </summary>
        public bool IsDad => m_RoleVisuals != null && m_RoleVisuals.IsDad;

        #endregion

        #region Unity & Network Lifecycle

        public void Initialize(CoreMovement motor)
        {
            m_Motor = motor;
        }

        private void Awake()
        {
            m_RoleVisuals = GetComponent<PlayerRoleVisuals>();
            m_StatsHandler = GetComponent<CoreStatsHandler>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            m_IsHoldingHands.OnValueChanged += OnHoldingStateChanged;
        }

        public override void OnNetworkDespawn()
        {
            m_IsHoldingHands.OnValueChanged -= OnHoldingStateChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsOwner) return;

            FindPartnerIfNeeded();

            if (m_PartnerTransform == null)
            {
                m_IsPartnerNearby = false;
                if (m_IsHoldingHands.Value)
                {
                    SetHoldingHands(false);
                }
                return;
            }

            float dist = Vector3.Distance(transform.position, m_PartnerTransform.position);
            m_IsPartnerNearby = dist <= interactDistance;

            // Xử lý đứt tay nếu xa quá
            if (m_IsHoldingHands.Value && dist > breakDistance)
            {
                SetHoldingHands(false);
            }

            // Xử lý phím bấm (Hỗ trợ cả Input System mới và cũ)
            bool togglePressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            {
                togglePressed = true;
            }
#else
            if (Input.GetKeyDown(holdHandKey))
            {
                togglePressed = true;
            }
#endif

            if (togglePressed && m_IsPartnerNearby)
            {
                SetHoldingHands(!m_IsHoldingHands.Value);
            }
        }

        #endregion

        #region Movement Processing

        public MovementModifier Process()
        {
            var modifier = new MovementModifier();

            if (!m_IsHoldingHands.Value || m_PartnerTransform == null)
            {
                return modifier;
            }

            // Nếu là Con (Child): Hưởng lợi từ tốc độ và lực kéo của Ba (Dad)
            if (!IsDad && m_PartnerHandAbility != null)
            {
                Vector3 toDad = m_PartnerTransform.position - transform.position;
                float currentDist = toDad.magnitude;

                if (currentDist > idealDistance)
                {
                    Vector3 pullDirection = toDad.normalized;
                    float catchupSpeed = Mathf.Max(m_PartnerHandAbility.m_Motor.CurrentSpeed, m_Motor.sprintSpeed);
                    modifier.ArealVelocity = pullDirection * (catchupSpeed * followSharpness * Time.deltaTime);
                }
            }
            // Nếu là Ba (Dad): Tiêu hao một chút thể lực khi kéo con
            else if (IsDad && m_Motor.IsSprinting && m_StatsHandler != null)
            {
                m_StatsHandler.TryConsumeStat(Animator.StringToHash("Stamina"), StaminaCost * Time.deltaTime);
            }

            return modifier;
        }

        public bool TryActivate()
        {
            if (m_IsPartnerNearby)
            {
                SetHoldingHands(!m_IsHoldingHands.Value);
                return true;
            }
            return false;
        }

        #endregion

        #region Internal Logic

        private void SetHoldingHands(bool isHolding)
        {
            if (m_IsHoldingHands.Value == isHolding) return;

            m_IsHoldingHands.Value = isHolding;

            if (isHolding)
            {
                UISoundManager.PlayClick();
                Debug.Log($"[HoldMyHand] 🤝 {(IsDad ? "Ba" : "Con")} đã nắm lấy bàn tay của {(IsDad ? "Con" : "Ba")}!");
            }
            else
            {
                Debug.Log($"[HoldMyHand] 💨 Đã buông tay.");
            }
        }

        private void OnHoldingStateChanged(bool previous, bool current)
        {
            // Có thể hook thêm VFX / Animation nắm tay ở đây
        }

        private void FindPartnerIfNeeded()
        {
            if (m_PartnerTransform != null) return;

            foreach (var player in PlayerRoleVisuals.AllPlayers)
            {
                if (player != null && player.gameObject != this.gameObject)
                {
                    m_PartnerTransform = player.transform;
                    m_PartnerHandAbility = player.GetComponent<CoopHoldHandAbility>();
                    break;
                }
            }
        }

        #endregion

        #region Debug UI

        private void OnGUI()
        {
            if (!IsOwner) return;

            // Hiển thị gợi ý phím trực quan trên màn hình
            if (m_IsPartnerNearby && !m_IsHoldingHands.Value)
            {
                string partnerName = IsDad ? "Con (👶)" : "Ba (🧔)";
                GUI.color = Color.yellow;
                GUI.Box(new Rect(Screen.width / 2f - 160, Screen.height - 120, 320, 35), 
                    $"🤝 Bấm [H] để Nắm Tay {partnerName}");
            }
            else if (m_IsHoldingHands.Value)
            {
                GUI.color = Color.cyan;
                GUI.Box(new Rect(Screen.width / 2f - 160, Screen.height - 120, 320, 35), 
                    $"🤝 Đang Nắm Tay! (Bấm [H] để buông tay)");
            }
        }

        #endregion
    }
}
