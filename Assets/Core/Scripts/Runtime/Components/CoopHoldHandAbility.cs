using System;
using UnityEngine;
using Unity.Netcode;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Cơ chế Nắm Tay (Hold My Hand) - Biểu tượng hợp tác cốt lõi của trò chơi:
    /// - Yêu cầu CẢ HAI nhân vật cùng giữ phím [H] trong 0.5 giây để kết nối nắm tay (trong Tutorial với Dummy thì người chơi giữ 0.5s là kết nối).
    /// - Khi đã nắm tay:
    ///   + Bàn tay của Ba và bàn tay của Con gắn liền trực tiếp với nhau (không dùng dây nối).
    ///   + Có hệ thống Stat Cân Nặng (Weight): Ba (~85kg) kéo Con (~28kg) đi rất khỏe và nhanh, Con chạy nước rút theo Ba;
    ///     Con kéo Ba thì rất chậm do trọng lượng Ba lớn.
    ///   + Bấm Shift chạy nước rút hoặc nhảy Space KHÔNG LÀM MẤT cơ chế nắm tay.
    ///   + Chỉ khi bấm tổ hợp phím [Ctrl + H] mới hủy buông tay (một phía đơn phương cũng hủy được).
    /// </summary>
    [RequireComponent(typeof(CoreMovement))]
    public class CoopHoldHandAbility : NetworkBehaviour, IMovementAbility
    {
        #region IMovementAbility Implementation

        public int Priority => 15; // Ưu tiên trên WalkAbility (0) và JumpAbility (10)
        public float StaminaCost => 1.0f; // Ba tiêu hao một lượng nhỏ thể lực khi chạy kéo Con

        #endregion

        #region Serialized Fields

        [Header("Thông số Cân Nặng (Weight Stat)")]
        [Tooltip("Khối lượng / Cân nặng nhân vật (kg). Ba: ~85kg, Con: ~28kg")]
        [SerializeField] private float weightKg = 85f;

        [Header("Khoảng cách tương tác & Lực kéo")]
        [Tooltip("Khoảng cách tối đa để có thể bắt đầu nắm tay (mét)")]
        [SerializeField] private float interactDistance = 2.8f;

        [Tooltip("Khoảng cách tối đa trước khi tự động tuột tay do bị kẹt/dịch chuyển (mét)")]
        [SerializeField] private float breakDistance = 5.5f;

        [Tooltip("Khoảng cách lý tưởng khi đi cạnh nhau (mét)")]
        [SerializeField] private float idealDistance = 1.15f;

        [Tooltip("Hệ số đàn hồi lực kéo theo")]
        [SerializeField] private float pullSpringStrength = 8.5f;

        [Header("Thời gian giữ phím kích hoạt")]
        [Tooltip("Thời gian cần giữ phím [H] để nắm tay (giây)")]
        [SerializeField] private float requiredHoldDuration = 0.5f;

#pragma warning disable CS0414
        [Header("Phím bấm")]
        [Tooltip("Phím mặc định để nắm tay")]
        [SerializeField] private KeyCode holdHandKey = KeyCode.H;
#pragma warning restore CS0414

        #endregion

        #region Network Variables

        // Đồng bộ trạng thái đang nắm tay
        private readonly NetworkVariable<bool> m_IsHoldingHands = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        // Đồng bộ trạng thái người chơi này đã giữ đủ 0.5s và sẵn sàng nắm tay
        private readonly NetworkVariable<bool> m_IsReadyToHold = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        #endregion

        #region Private Fields

        private CoreMovement m_Motor;
        private PlayerRoleVisuals m_RoleVisuals;
        private CoreStatsHandler m_StatsHandler;
        private Animator m_Animator;
        private CharacterController m_CharacterController;

        private CoopHoldHandAbility m_PartnerHandAbility;
        private Transform m_PartnerTransform;
        private Animator m_PartnerAnimator;
        private bool m_IsPartnerNearby = false;

        // Xương cánh tay (UpperArm, LowerArm, Hand) để áp dụng 2-Bone IK
        private Transform m_RightUpperArm;
        private Transform m_RightLowerArm;
        private Transform m_RightHand;

        private Transform m_LeftUpperArm;
        private Transform m_LeftLowerArm;
        private Transform m_LeftHand;

        private Transform m_PartnerRightUpperArm;
        private Transform m_PartnerRightLowerArm;
        private Transform m_PartnerRightHand;

        private Transform m_PartnerLeftUpperArm;
        private Transform m_PartnerLeftLowerArm;
        private Transform m_PartnerLeftHand;

        // Bộ đếm thời gian giữ [H]
        private float m_CurrentHoldTimer = 0f;
        private bool m_IsHoldingKeyH = false;

        // Thông báo trạng thái trên màn hình
        private string m_StatusAlert = "";
        private float m_StatusAlertTimer = 0f;

        private static readonly int s_IsHoldingHandsHash = Animator.StringToHash("IsHoldingHands");

        #endregion

        #region Properties

        public bool IsLocalPlayerCharacter
        {
            get
            {
                if (GetComponent<SoloCompanionDummy>() != null) return false;

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
                {
                    var localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
                    if (localPlayerObj != null)
                    {
                        return localPlayerObj.gameObject == this.gameObject;
                    }
                }

                return IsOwner;
            }
        }

        private bool m_IsLocalHoldingHands = false;

        public bool IsHoldingHands
        {
            get
            {
                if (m_IsLocalHoldingHands || m_IsHoldingHands.Value) return true;
                if (m_PartnerHandAbility != null && (m_PartnerHandAbility.m_IsLocalHoldingHands || m_PartnerHandAbility.m_IsHoldingHands.Value)) return true;
                return false;
            }
        }

        public bool IsDad
        {
            get
            {
                if (m_RoleVisuals != null) return m_RoleVisuals.IsDad;
                string objName = gameObject.name.ToLower();
                if (objName.Contains("child") || objName.Contains("son")) return false;
                if (objName.Contains("dad")) return true;
                return weightKg > 50f;
            }
        }

        public float WeightKg => weightKg;

        public float CurrentMoveSpeed => m_Motor != null ? m_Motor.CurrentSpeed : 0f;
        public bool IsSprinting => m_Motor != null && m_Motor.IsSprinting;
        public bool IsCrouching => m_Motor != null && m_Motor.IsCrouching;

        public float PartnerMoveSpeed
        {
            get
            {
                if (m_PartnerHandAbility != null) return m_PartnerHandAbility.CurrentMoveSpeed;
                if (m_PartnerTransform != null)
                {
                    var motor = m_PartnerTransform.GetComponent<CoreMovement>();
                    if (motor != null) return motor.CurrentSpeed;
                }
                return 0f;
            }
        }

        public bool IsPartnerSprinting
        {
            get
            {
                if (m_PartnerHandAbility != null) return m_PartnerHandAbility.IsSprinting;
                if (m_PartnerTransform != null)
                {
                    var motor = m_PartnerTransform.GetComponent<CoreMovement>();
                    if (motor != null) return motor.IsSprinting;
                }
                return false;
            }
        }

        public bool IsPartnerCrouching
        {
            get
            {
                if (m_PartnerHandAbility != null) return m_PartnerHandAbility.IsCrouching;
                if (m_PartnerTransform != null)
                {
                    var motor = m_PartnerTransform.GetComponent<CoreMovement>();
                    if (motor != null) return motor.IsCrouching;
                }
                return false;
            }
        }

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
            m_Animator = GetComponentInChildren<Animator>();
            m_CharacterController = GetComponent<CharacterController>();
            if (m_Motor == null) m_Motor = GetComponent<CoreMovement>();

            // Gán trọng lượng theo vai trò: Ba ~85kg, Con ~28kg
            weightKg = IsDad ? 85.0f : 28.0f;

            CacheHandBones();
            RemoveLegacyTetherLine();
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
            if (m_StatusAlertTimer > 0f)
            {
                m_StatusAlertTimer -= Time.deltaTime;
                if (m_StatusAlertTimer <= 0f) m_StatusAlert = "";
            }

            FindPartnerIfNeeded();

            // CHỈ NGƯỜI CHƠI THẬT MỚI XỬ LÝ PHÍM BẤM
            if (!IsLocalPlayerCharacter) return;

            if (m_PartnerTransform == null)
            {
                m_IsPartnerNearby = false;
                if (IsHoldingHands)
                {
                    SetHoldingHands(false);
                }
                return;
            }

            float dist = Vector3.Distance(transform.position, m_PartnerTransform.position);
            m_IsPartnerNearby = dist <= interactDistance;

            // Nhận diện Input phím H và phím Ctrl
            bool hHeld = false;
            bool hPressedThisFrame = false;
            bool ctrlHeld = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                hHeld = Keyboard.current.hKey.isPressed;
                hPressedThisFrame = Keyboard.current.hKey.wasPressedThisFrame;
                ctrlHeld = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
            }
#else
            hHeld = Input.GetKey(holdHandKey) || Input.GetKey(KeyCode.H);
            hPressedThisFrame = Input.GetKeyDown(holdHandKey) || Input.GetKeyDown(KeyCode.H);
            ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
#endif

            // ==============================================================
            // TRƯỜNG HỢP 1: ĐANG NẮM TAY
            // ==============================================================
            if (IsHoldingHands)
            {
                // BẮT BUỘC BẤM CTRL + H ĐỂ BUÔNG TAY (Đơn phương 1 phía cũng buông được)
                if (ctrlHeld && hPressedThisFrame)
                {
                    SetHoldingHands(false);
                    ShowAlert("💨 Đã buông tay (Ctrl + H)", 2.0f);
                    UISoundManager.PlayClick();
                    return;
                }

                // Nếu đi quá xa (> breakDistance) do dịch chuyển hoặc kẹt thì tuột tay an toàn
                if (dist > breakDistance)
                {
                    SetHoldingHands(false);
                    ShowAlert("💔 TUỘT TAY! Khoảng cách quá xa!", 2.5f);
                    UISoundManager.PlayClick();
                    return;
                }

                // Nếu đối phương là Dummy Bot trong chế độ Tutorial: Kéo trực tiếp CharacterController của Dummy theo
                PullCompanionDummyIfNeeded();
            }
            // ==============================================================
            // TRƯỜNG HỢP 2: CHƯA NẮM TAY -> CẦN GIỮ H TRONG 0.5 GIÂY ĐỂ NẮM
            // ==============================================================
            else
            {
                if (hHeld && m_IsPartnerNearby)
                {
                    m_IsHoldingKeyH = true;
                    m_CurrentHoldTimer += Time.deltaTime;

                    if (m_CurrentHoldTimer >= requiredHoldDuration)
                    {
                        // Người chơi này đã sẵn sàng
                        SetLocalReadyState(true);

                        // Kiểm tra xem đối tác đã sẵn sàng chưa
                        bool partnerReady = false;
                        if (m_PartnerHandAbility != null)
                        {
                            // Nếu đối phương là Companion Dummy -> Tự động chấp nhận ngay
                            if (m_PartnerTransform.GetComponent<SoloCompanionDummy>() != null)
                            {
                                partnerReady = true;
                            }
                            else
                            {
                                partnerReady = m_PartnerHandAbility.m_IsReadyToHold.Value;
                            }
                        }

                        if (partnerReady)
                        {
                            // Cả hai cùng sẵn sàng -> KẾT NỐI NẮM TAY!
                            SetHoldingHands(true);
                            SetLocalReadyState(false);
                            m_CurrentHoldTimer = 0f;
                            m_IsHoldingKeyH = false;
                        }
                    }
                }
                else
                {
                    if (m_IsHoldingKeyH)
                    {
                        m_CurrentHoldTimer = 0f;
                        m_IsHoldingKeyH = false;
                        SetLocalReadyState(false);
                    }
                }
            }
        }

        private void LateUpdate()
        {
            // ==============================================================
            // GẮN LIỀN TRỰC TIẾP HAI BÀN TAY BẰNG 2-BONE IK CHUẨN XÁC
            // (Chỉ 1 tay nắm, không kéo dãn xương, triệt tiêu anim chạy trên tay nắm)
            // ==============================================================
            if (!IsHoldingHands || m_PartnerTransform == null) return;

            // Mỗi client chỉ chạy căn chỉnh IK 1 lần thông qua nhân vật của local player
            if (!IsLocalPlayerCharacter) return;

            AlignClaspedHandsDirectly();
        }

        #endregion

        #region Direct Hand Clasping (Gắn Liền Trực Tiếp Hai Bàn Tay Bằng 2-Bone IK)

        private void CacheHandBones()
        {
            if (m_Animator == null) m_Animator = GetComponentInChildren<Animator>();
            if (m_Animator != null && m_Animator.isHuman)
            {
                m_RightUpperArm = m_Animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                m_RightLowerArm = m_Animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                m_RightHand = m_Animator.GetBoneTransform(HumanBodyBones.RightHand);

                m_LeftUpperArm = m_Animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                m_LeftLowerArm = m_Animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                m_LeftHand = m_Animator.GetBoneTransform(HumanBodyBones.LeftHand);
            }
        }

        private void CachePartnerHandBones()
        {
            if (m_PartnerTransform == null) return;
            if (m_PartnerAnimator == null) m_PartnerAnimator = m_PartnerTransform.GetComponentInChildren<Animator>();
            if (m_PartnerAnimator != null && m_PartnerAnimator.isHuman)
            {
                m_PartnerRightUpperArm = m_PartnerAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                m_PartnerRightLowerArm = m_PartnerAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                m_PartnerRightHand = m_PartnerAnimator.GetBoneTransform(HumanBodyBones.RightHand);

                m_PartnerLeftUpperArm = m_PartnerAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                m_PartnerLeftLowerArm = m_PartnerAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                m_PartnerLeftHand = m_PartnerAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
            }
        }

        private void RemoveLegacyTetherLine()
        {
            var oldTether = transform.Find("HandBondTether");
            if (oldTether != null)
            {
                Destroy(oldTether.gameObject);
            }
        }

        /// <summary>
        /// Gắn liền trực tiếp bàn tay của Ba và Con bằng giải thuật 2-Bone IK.
        /// Chỉ nắm 1 tay cùng lúc, giữ nguyên chiều dài xương (không dãn mesh cổ tay),
        /// và giữ tay nắm hướng về phía nhau mà không bị animation chạy làm vung vẩy.
        /// </summary>
        private void AlignClaspedHandsDirectly()
        {
            if (m_RightUpperArm == null || m_LeftUpperArm == null) CacheHandBones();
            if (m_PartnerRightUpperArm == null || m_PartnerLeftUpperArm == null) CachePartnerHandBones();

            Transform dadT = IsDad ? transform : m_PartnerTransform;
            Transform childT = IsDad ? m_PartnerTransform : transform;

            Transform dadRUpper = IsDad ? m_RightUpperArm : m_PartnerRightUpperArm;
            Transform dadRLower = IsDad ? m_RightLowerArm : m_PartnerRightLowerArm;
            Transform dadRHand  = IsDad ? m_RightHand : m_PartnerRightHand;

            Transform dadLUpper = IsDad ? m_LeftUpperArm : m_PartnerLeftUpperArm;
            Transform dadLLower = IsDad ? m_LeftLowerArm : m_PartnerLeftLowerArm;
            Transform dadLHand  = IsDad ? m_LeftHand : m_PartnerLeftHand;

            Transform childRUpper = IsDad ? m_PartnerRightUpperArm : m_RightUpperArm;
            Transform childRLower = IsDad ? m_PartnerRightLowerArm : m_RightLowerArm;
            Transform childRHand  = IsDad ? m_PartnerRightHand : m_RightHand;

            Transform childLUpper = IsDad ? m_PartnerLeftUpperArm : m_LeftUpperArm;
            Transform childLLower = IsDad ? m_PartnerLeftLowerArm : m_LeftLowerArm;
            Transform childLHand  = IsDad ? m_PartnerLeftHand : m_LeftHand;

            if (dadRUpper == null || dadLUpper == null || childRUpper == null || childLUpper == null) return;

            // Xác định xem Con đang ở sườn bên phải hay bên trái của Ba
            Vector3 childLocalInDad = dadT.InverseTransformPoint(childT.position);
            bool childOnRight = childLocalInDad.x >= 0f;

            // CHỈ NẮM 1 TAY CÙNG LÚC:
            // - Con ở bên phải Ba: Ba dùng TAY PHẢI, Con dùng TAY TRÁI (tay còn lại vẫn đánh tay tự nhiên khi chạy)
            // - Con ở bên trái Ba: Ba dùng TAY TRÁI, Con dùng TAY PHẢI (tay còn lại vẫn đánh tay tự nhiên khi chạy)
            Transform dadUpper = childOnRight ? dadRUpper : dadLUpper;
            Transform dadLower = childOnRight ? dadRLower : dadLLower;
            Transform dadHand  = childOnRight ? dadRHand  : dadLHand;

            Transform childUpper = childOnRight ? childLUpper : childRUpper;
            Transform childLower = childOnRight ? childLLower : childRLower;
            Transform childHand  = childOnRight ? childLHand  : childRHand;

            Vector3 dadSideDir = childOnRight ? dadT.right : -dadT.right;

            // Điểm nắm tay tự nhiên ngang hông của Ba (cao khoảng 0.90m)
            Vector3 dadHipPoint = dadT.position + Vector3.up * 0.90f + dadSideDir * 0.28f + dadT.forward * 0.05f;
            Vector3 toChildFromHip = childUpper.position - dadHipPoint;
            Vector3 contactPoint = dadHipPoint + Vector3.ClampMagnitude(toChildFromHip * 0.45f, 0.45f);
            contactPoint.y = Mathf.Clamp(contactPoint.y, dadT.position.y + 0.82f, dadT.position.y + 0.98f);

            // Pole hint hướng cùi chỏ gấp tự nhiên:
            // Ba: Cùi chỏ gập tự nhiên ra ngoài và hơi lùi về sau
            Vector3 dadPole = dadUpper.position + dadSideDir * 0.45f - dadT.forward * 0.25f;
            // Con: Cùi chỏ gập tự nhiên ra ngoài và hơi chúc xuống
            Vector3 childPole = childUpper.position - dadSideDir * 0.35f - Vector3.up * 0.30f;

            // GIẢI THUẬT 2-BONE IK CHUẨN XÁC:
            // Tuyệt đối không kéo dãn cổ tay (mesh không bị dãn), chỉ xoay khớp vai và cùi chỏ
            // Triệt tiêu animation đánh tay trên tay nắm khi chạy, trong khi tay còn lại vẫn đánh tay tự nhiên
            // Giữ cổ tay tự nhiên (không xoay quắp ngược ra sau lưng hay trẹo khớp)
            SolveTwoBoneIK(dadUpper, dadLower, dadHand, contactPoint, dadPole);
            SolveTwoBoneIK(childUpper, childLower, childHand, contactPoint, childPole);
        }

        private static void SolveTwoBoneIK(
            Transform upperArm,
            Transform lowerArm,
            Transform hand,
            Vector3 targetPos,
            Vector3 poleHint)
        {
            if (upperArm == null || lowerArm == null || hand == null) return;

            Vector3 p0 = upperArm.position;
            Vector3 p1 = lowerArm.position;
            Vector3 p2 = hand.position;

            float l1 = Vector3.Distance(p0, p1);
            float l2 = Vector3.Distance(p1, p2);
            if (l1 < 0.01f || l2 < 0.01f) return;

            float d = Vector3.Distance(p0, targetPos);
            float minReach = Mathf.Abs(l1 - l2) + 0.005f;
            float maxReach = (l1 + l2) * 0.998f;
            d = Mathf.Clamp(d, minReach, maxReach);

            Vector3 uTarget = (targetPos - p0).normalized;
            Vector3 clampedTarget = p0 + uTarget * d;

            // Law of Cosines
            float cosAlpha = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            float alpha = Mathf.Acos(cosAlpha) * Mathf.Rad2Deg;

            Vector3 toPole = (poleHint - p0).normalized;
            Vector3 bendNormal = Vector3.Cross(uTarget, toPole);
            if (bendNormal.sqrMagnitude < 0.001f)
            {
                bendNormal = Vector3.Cross(uTarget, Vector3.up);
                if (bendNormal.sqrMagnitude < 0.001f)
                {
                    bendNormal = Vector3.Cross(uTarget, Vector3.right);
                }
            }
            bendNormal.Normalize();

            Vector3 elbowDir = Quaternion.AngleAxis(alpha, bendNormal) * uTarget;

            // 1. Xoay Upper Arm hướng về cùi chỏ mới
            Vector3 curUpperDir = (lowerArm.position - upperArm.position).normalized;
            upperArm.rotation = Quaternion.FromToRotation(curUpperDir, elbowDir) * upperArm.rotation;

            // 2. Xoay Lower Arm hướng về điểm nắm tay (dựa trên vị trí đã cập nhật của cùi chỏ)
            Vector3 curLowerDir = (hand.position - lowerArm.position).normalized;
            Vector3 desiredLowerDir = (clampedTarget - lowerArm.position).normalized;
            lowerArm.rotation = Quaternion.FromToRotation(curLowerDir, desiredLowerDir) * lowerArm.rotation;

            // 3. Khóa góc xoay cổ tay về tư thế tự nhiên thẳng theo cẳng tay (tránh bị quắp ngược hoặc vặn xoắn)
            hand.localRotation = Quaternion.identity;
        }

        #endregion

        #region Pulling Dynamics & Weight Stat (Cân Nặng & Lực Kéo)

        public MovementModifier Process()
        {
            var modifier = new MovementModifier();

            if (!IsHoldingHands || m_PartnerTransform == null)
            {
                return modifier;
            }

            Vector3 toPartner = m_PartnerTransform.position - transform.position;
            Vector3 flatToPartner = Vector3.ProjectOnPlane(toPartner, Vector3.up);
            float currentDist = flatToPartner.magnitude;

            if (currentDist > idealDistance)
            {
                Vector3 pullDir = flatToPartner.normalized;
                float stretch = currentDist - idealDistance;

                // ==============================================================
                // VẬT LÝ KÉO DỰA TRÊN CÂN NẶNG:
                // Ba: ~85kg (Rất khỏe, kéo mạnh)
                // Con: ~28kg (Nhẹ, bị kéo bay theo Ba; Con kéo Ba thì Ba chỉ đi chậm)
                // ==============================================================
                if (!IsDad)
                {
                    // CON (28kg): Ba (85kg) kéo Con đi rất khỏe và nhanh
                    float dadSpeed = (m_PartnerHandAbility != null && m_PartnerHandAbility.m_Motor != null)
                        ? m_PartnerHandAbility.m_Motor.CurrentSpeed
                        : 4.0f;

                    // Con bắt kịp tốc độ của Ba (kể cả khi Ba chạy nước rút Sprint) + lực kéo lò xo
                    float catchupSpeed = Mathf.Max(dadSpeed, 3.8f) + (stretch * pullSpringStrength);
                    modifier.ArealVelocity = pullDir * catchupSpeed;
                }
                else
                {
                    // BA (85kg): Con (28kg) kéo Ba thì Ba chỉ đi chậm (do Ba nặng hơn gấp 3 lần)
                    float childSpeed = (m_PartnerHandAbility != null && m_PartnerHandAbility.m_Motor != null)
                        ? m_PartnerHandAbility.m_Motor.CurrentSpeed
                        : 0f;

                    float weightRatio = 28.0f / 85.0f; // ~0.33
                    float dragSpeed = childSpeed * weightRatio * 0.35f; // Kéo chậm
                    modifier.ArealVelocity = pullDir * (dragSpeed * Mathf.Min(stretch, 1.0f));
                }
            }

            // Tiêu hao thể lực nhẹ của Ba khi chạy kéo Con
            if (IsDad && m_Motor.IsSprinting && m_StatsHandler != null)
            {
                m_StatsHandler.TryConsumeStat(Animator.StringToHash("Stamina"), StaminaCost * Time.deltaTime);
            }

            return modifier;
        }

        private void PullCompanionDummyIfNeeded()
        {
            if (m_PartnerTransform == null) return;

            var dummy = m_PartnerTransform.GetComponent<SoloCompanionDummy>();
            if (dummy == null) return;

            var dummyCC = m_PartnerTransform.GetComponent<CharacterController>();
            if (dummyCC == null || !dummyCC.enabled) return;

            if (m_PartnerAnimator == null)
            {
                m_PartnerAnimator = m_PartnerTransform.GetComponentInChildren<Animator>();
            }

            // Xác định xem Dummy đang ở sườn bên phải hay sườn bên trái của Ba
            Vector3 toDummyLocal = transform.InverseTransformPoint(m_PartnerTransform.position);
            bool dummyOnRight = toDummyLocal.x >= 0f;
            Vector3 sideDir = dummyOnRight ? transform.right : -transform.right;

            // Vị trí sườn tự nhiên: cách Ba 0.82m về phía hông và hơi lùi 0.25m
            Vector3 targetFlankPos = transform.position + sideDir * 0.82f - transform.forward * 0.25f;
            Vector3 toFlank = Vector3.ProjectOnPlane(targetFlankPos - m_PartnerTransform.position, Vector3.up);
            float dist = toFlank.magnitude;

            float leaderSpeed = m_Motor != null ? m_Motor.CurrentSpeed : 0f;
            bool isLeaderMoving = leaderSpeed > 0.15f;
            bool isLeaderSprinting = m_Motor != null && m_Motor.IsSprinting;
            bool isLeaderCrouching = m_Motor != null && m_Motor.IsCrouching;
            bool isLeaderGrounded = m_Motor != null && m_Motor.IsGrounded;

            if (dist > 0.05f)
            {
                float baseSpeed = IsDad ? Mathf.Max(leaderSpeed, 3.8f) : (leaderSpeed * 0.35f);
                float dummyPullSpeed = baseSpeed + (dist * pullSpringStrength);
                dummyCC.Move(toFlank.normalized * (dummyPullSpeed * Time.deltaTime));

                // Đồng thời quay mặt Dummy về hướng Ba đang di chuyển
                if (leaderSpeed > 0.1f)
                {
                    m_PartnerTransform.rotation = Quaternion.Slerp(m_PartnerTransform.rotation, transform.rotation, Time.deltaTime * 12f);
                }
            }

            // ==============================================================
            // ĐỒNG BỘ ANIMATION SỐNG ĐỘNG CHO BẠN ĐỒNG HÀNH (DUMMY BOT):
            // Giúp toàn bộ cơ thể (2 chân, tay tự do, thân người) chạy nhảy cùng nhịp với Ba
            // ==============================================================
            if (m_PartnerAnimator != null)
            {
                if (isLeaderMoving)
                {
                    // Tốc độ chạy của Con: nếu Ba chạy (sprint), Con chạy hết tốc lực (6.0f); nếu Ba đi bộ, Con đi bộ (3.0f - 3.5f)
                    float targetAnimSpeed = isLeaderSprinting ? Mathf.Max(leaderSpeed, 6.0f) : Mathf.Max(leaderSpeed, 3.2f);
                    if (isLeaderCrouching)
                    {
                        targetAnimSpeed = Mathf.Min(targetAnimSpeed, 2.2f);
                    }

                    float curAnimSpeed = m_PartnerAnimator.GetFloat("Speed");
                    float smoothedSpeed = Mathf.Lerp(curAnimSpeed, targetAnimSpeed, Time.deltaTime * 12f);

                    m_PartnerAnimator.SetFloat("Speed", smoothedSpeed);
                    m_PartnerAnimator.SetFloat("MotionSpeed", 1.0f);
                    m_PartnerAnimator.SetBool("Grounded", isLeaderGrounded);
                    m_PartnerAnimator.SetBool("IsCrouching", isLeaderCrouching);
                    m_PartnerAnimator.SetBool("IsSitting", isLeaderCrouching);
                    m_PartnerAnimator.SetBool("Jump", false);
                    m_PartnerAnimator.SetBool("FreeFall", false);
                }
                else
                {
                    // Ba dừng lại -> Con giảm dần tốc độ về 0 và đứng lại (Idle) tự nhiên
                    float curAnimSpeed = m_PartnerAnimator.GetFloat("Speed");
                    float smoothedSpeed = Mathf.Lerp(curAnimSpeed, 0f, Time.deltaTime * 14f);
                    if (smoothedSpeed < 0.05f) smoothedSpeed = 0f;

                    m_PartnerAnimator.SetFloat("Speed", smoothedSpeed);
                    m_PartnerAnimator.SetBool("Grounded", isLeaderGrounded);
                    m_PartnerAnimator.SetBool("IsCrouching", isLeaderCrouching);
                    m_PartnerAnimator.SetBool("IsSitting", isLeaderCrouching);
                }
            }
        }

        public bool TryActivate() => false;

        #endregion

        #region Internal Logic & Synchronization

        public void SetHoldingHandsDirect(bool isHolding)
        {
            m_IsLocalHoldingHands = isHolding;
            if (IsOwner && IsSpawned)
            {
                try
                {
                    m_IsHoldingHands.Value = isHolding;
                }
                catch (System.Exception) {}
            }
            UpdateAnimatorState(isHolding);
        }

        private void SetHoldingHands(bool isHolding)
        {
            if (m_IsLocalHoldingHands == isHolding && (!IsSpawned || !IsOwner || m_IsHoldingHands.Value == isHolding)) return;

            m_IsLocalHoldingHands = isHolding;

            if (IsOwner && IsSpawned)
            {
                try
                {
                    m_IsHoldingHands.Value = isHolding;
                }
                catch (System.Exception) {}
            }

            if (IsSpawned)
            {
                SyncHandHoldingRpc(isHolding);
            }

            if (m_PartnerHandAbility != null)
            {
                m_PartnerHandAbility.m_IsLocalHoldingHands = isHolding;
            }

            UpdateAnimatorState(isHolding);

            if (isHolding)
            {
                UISoundManager.PlayClick();
                Debug.Log($"[HoldMyHand] 🤝 {(IsDad ? "Ba" : "Con")} đã nắm chặt tay {(IsDad ? "Con" : "Ba")}!");
            }
            else
            {
                Debug.Log($"[HoldMyHand] 💨 Đã buông tay.");
            }
        }

        [Rpc(SendTo.NotMe)]
        private void SyncHandHoldingRpc(bool isHolding)
        {
            m_IsLocalHoldingHands = isHolding;
            UpdateAnimatorState(isHolding);

            if (m_PartnerHandAbility != null)
            {
                m_PartnerHandAbility.SetHoldingHandsDirect(isHolding);
            }
            else
            {
                FindPartnerIfNeeded();
                if (m_PartnerHandAbility != null)
                {
                    m_PartnerHandAbility.SetHoldingHandsDirect(isHolding);
                }
            }

            if (isHolding)
            {
                UISoundManager.PlayClick();
                Debug.Log($"[HoldMyHand] 🤝 Đã nhận tín hiệu nắm tay từ đối tác!");
            }
            else
            {
                ShowAlert("💨 Đối tác đã buông tay", 2.0f);
                Debug.Log($"[HoldMyHand] 💨 Đối tác đã buông tay.");
            }
        }

        private void SetLocalReadyState(bool ready)
        {
            if (IsOwner && IsSpawned)
            {
                try
                {
                    m_IsReadyToHold.Value = ready;
                }
                catch (System.Exception) {}
            }
        }

        private void OnHoldingStateChanged(bool previous, bool current)
        {
            m_IsLocalHoldingHands = current;
            UpdateAnimatorState(current);
            if (!current)
            {
                m_CurrentHoldTimer = 0f;
                m_IsHoldingKeyH = false;
            }
        }

        private void UpdateAnimatorState(bool isHolding)
        {
            if (m_Animator != null)
            {
                m_Animator.SetBool(s_IsHoldingHandsHash, isHolding);
            }
            if (m_PartnerAnimator != null)
            {
                m_PartnerAnimator.SetBool(s_IsHoldingHandsHash, isHolding);
                if (!isHolding)
                {
                    m_PartnerAnimator.SetFloat("Speed", 0f);
                }
            }
        }

        private void ShowAlert(string msg, float duration)
        {
            m_StatusAlert = msg;
            m_StatusAlertTimer = duration;
        }

        private void FindPartnerIfNeeded()
        {
            if (m_PartnerTransform != null && m_PartnerHandAbility != null && m_PartnerAnimator != null) return;

            // 1. Quét tìm Dummy Companion trước (chế độ Tutorial)
            var dummy = FindAnyObjectByType<SoloCompanionDummy>();
            if (dummy != null && dummy.gameObject != this.gameObject)
            {
                m_PartnerTransform = dummy.transform;
                m_PartnerHandAbility = dummy.GetComponent<CoopHoldHandAbility>();
                m_PartnerAnimator = dummy.GetComponentInChildren<Animator>();
                CachePartnerHandBones();
                return;
            }

            // 2. Quét qua ConnectedClients trong NetworkManager (Multiplayer PlayScene1)
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClientsList != null)
            {
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    if (client != null && client.PlayerObject != null && client.PlayerObject.gameObject != this.gameObject)
                    {
                        m_PartnerTransform = client.PlayerObject.transform;
                        m_PartnerHandAbility = client.PlayerObject.GetComponent<CoopHoldHandAbility>();
                        m_PartnerAnimator = client.PlayerObject.GetComponentInChildren<Animator>();
                        CachePartnerHandBones();
                        return;
                    }
                }
            }

            // 3. Quét qua AllPlayers
            foreach (var player in PlayerRoleVisuals.AllPlayers)
            {
                if (player != null && player.gameObject != this.gameObject)
                {
                    m_PartnerTransform = player.transform;
                    m_PartnerHandAbility = player.GetComponent<CoopHoldHandAbility>();
                    m_PartnerAnimator = player.GetComponentInChildren<Animator>();
                    CachePartnerHandBones();
                    return;
                }
            }

            // 4. Fallback: Quét các component CoopHoldHandAbility trong Scene
            var allAbilities = FindObjectsByType<CoopHoldHandAbility>(FindObjectsInactive.Exclude);
            foreach (var ability in allAbilities)
            {
                if (ability != null && ability.gameObject != this.gameObject)
                {
                    m_PartnerTransform = ability.transform;
                    m_PartnerHandAbility = ability;
                    m_PartnerAnimator = ability.GetComponentInChildren<Animator>();
                    CachePartnerHandBones();
                    return;
                }
            }
        }

        #endregion

        #region UI Prompt & Feedback (Vòng Tròn Load Tối Giản)

        private void OnGUI()
        {
            if (!IsLocalPlayerCharacter) return;

            // VÒNG TRÒN LOAD TIẾN TRÌNH GIỮ [H] (Circular Radial Loading Ring)
            // Chỉ hiển thị khi đang nhấn giữ [H] để nạp kết nối nắm tay, không dùng ô chữ to
            if (m_IsHoldingKeyH && m_IsPartnerNearby && !IsHoldingHands)
            {
                float progress = Mathf.Clamp01(m_CurrentHoldTimer / requiredHoldDuration);
                DrawCircularLoadRing(progress);
            }
        }

        private void DrawCircularLoadRing(float progress)
        {
            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.52f;
            float radius = 26f;
            int numDots = 28;
            float dotSize = 4f;

            Color filledColor = new Color(0.2f, 0.95f, 0.85f, 0.95f); // Cyan phát sáng
            Color emptyColor = new Color(0.12f, 0.16f, 0.22f, 0.45f);  // Xám mờ nền

            // Vẽ các chấm tròn theo chu vi đồng hồ 360 độ
            for (int i = 0; i < numDots; i++)
            {
                float stepRatio = i / (float)numDots;
                float angle = -90f + (stepRatio * 360f); // Bắt đầu từ 12 giờ xoay theo chiều kim đồng hồ
                float rad = angle * Mathf.Deg2Rad;

                float x = centerX + Mathf.Cos(rad) * radius - dotSize * 0.5f;
                float y = centerY + Mathf.Sin(rad) * radius - dotSize * 0.5f;

                GUI.color = (stepRatio <= progress) ? filledColor : emptyColor;
                GUI.DrawTexture(new Rect(x, y, dotSize, dotSize), Texture2D.whiteTexture);
            }

            // Chữ "H" nhỏ thanh lịch tinh gọn ngay tâm vòng tròn
            var centerStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            centerStyle.normal.textColor = Color.white;
            GUI.color = Color.white;
            GUI.Label(new Rect(centerX - 15f, centerY - 12f, 30f, 24f), "H", centerStyle);
        }

        #endregion
    }
}
