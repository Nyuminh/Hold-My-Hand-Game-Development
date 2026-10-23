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
    ///   + Có Dây Năng Lượng / Cáp Liên Kết phát sáng (Golden Bond Tether) kết nối 2 bàn tay.
    ///   + Tốc độ di chuyển của Con tự động tăng lên bằng tốc độ của Ba (kéo con chạy cùng).
    ///   + Ba dùng thể lực dìu dắt, giúp Con không bị kiệt sức (Child stamina drain = 0).
    ///   + Giúp cả hai cùng vượt qua các luồng gió cản trở hoặc dòng nước xiết.
    ///   + Tự động tách tay khi khoảng cách vượt quá giới hạn an toàn kèm hiệu ứng âm thanh cảnh báo.
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
        [SerializeField] private float interactDistance = 2.6f;

        [Tooltip("Khoảng cách tối đa trước khi tự động tuột tay (mét)")]
        [SerializeField] private float breakDistance = 3.6f;

        [Tooltip("Khoảng cách lý tưởng khi đi cạnh nhau")]
        [SerializeField] private float idealDistance = 1.1f;

        [Tooltip("Độ mượt khi kéo theo")]
        [SerializeField] private float followSharpness = 8.5f;

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

        // Visual Hand Tether Line Renderer
        private LineRenderer m_TetherLine;
        private Transform m_MyHandTransform;
        private Transform m_PartnerHandTransform;
        private Transform m_VisualModel;
        private Quaternion m_DefaultModelRot = Quaternion.identity;

        // Snap break message
        private string m_BreakAlert = "";
        private float m_BreakAlertTimer = 0f;

        #endregion

        #region Properties

        /// <summary>
        /// Kiểm tra xem instance này có phải nhân vật do NGƯỜI CHƠI THẬT ĐIỀU KHIỂN không (loại trừ Dummy Companion).
        /// </summary>
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

        /// <summary>
        /// Cho biết nhân vật có đang trong trạng thái nắm tay hay không.
        /// </summary>
        public bool IsHoldingHands => m_IsHoldingHands.Value;

        /// <summary>
        /// Kiểm tra xem nhân vật này là Ba hay Con.
        /// </summary>
        public bool IsDad => m_RoleVisuals != null ? m_RoleVisuals.IsDad : gameObject.name.ToLower().Contains("dad");

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
            if (m_Motor == null) m_Motor = GetComponent<CoreMovement>();

            FindVisualModel();
            SetupTetherLine();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            m_IsHoldingHands.OnValueChanged += OnHoldingStateChanged;
            UpdateTetherVisibility(m_IsHoldingHands.Value);
        }

        public override void OnNetworkDespawn()
        {
            m_IsHoldingHands.OnValueChanged -= OnHoldingStateChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (m_BreakAlertTimer > 0f)
            {
                m_BreakAlertTimer -= Time.deltaTime;
                if (m_BreakAlertTimer <= 0f) m_BreakAlert = "";
            }

            FindPartnerIfNeeded();

            // Cập nhật vị trí và hình ảnh dây kết nối (cả owner và remote)
            UpdateTetherAnimation();

            // CHỈ NGƯỜI CHƠI THẬT MỚI XỬ LÝ PHÍM BẤM NẮM TAY
            if (!IsLocalPlayerCharacter) return;

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

            // Xử lý tuột tay nếu khoảng cách quá xa
            if (m_IsHoldingHands.Value && dist > breakDistance)
            {
                SetHoldingHands(false);
                m_BreakAlert = "💔 TUỘT TAY! Hai cha con đã đi quá xa nhau!";
                m_BreakAlertTimer = 2.5f;
                UISoundManager.PlayClick();
            }

            // Xử lý phím bấm H
            bool togglePressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            {
                togglePressed = true;
            }
#else
            if (Input.GetKeyDown(holdHandKey) || Input.GetKeyDown(KeyCode.H))
            {
                togglePressed = true;
            }
#endif

            if (togglePressed && m_IsPartnerNearby)
            {
                SetHoldingHands(!m_IsHoldingHands.Value);
            }
        }

        private void OnDisable()
        {
            if (m_TetherLine != null) m_TetherLine.enabled = false;
        }

        #endregion

        #region Visual Tether & Procedural Animations

        private void SetupTetherLine()
        {
            var tetherObj = new GameObject("HandBondTether");
            tetherObj.transform.SetParent(transform, false);

            m_TetherLine = tetherObj.AddComponent<LineRenderer>();
            m_TetherLine.useWorldSpace = true;
            m_TetherLine.positionCount = 14;
            m_TetherLine.startWidth = 0.08f;
            m_TetherLine.endWidth = 0.08f;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = new Color(1f, 0.78f, 0.15f, 0.95f); // Ánh vàng mật ong
                m_TetherLine.material = mat;
            }

            m_TetherLine.startColor = new Color(1f, 0.85f, 0.2f, 1f);
            m_TetherLine.endColor = new Color(1f, 0.65f, 0.1f, 1f);
            m_TetherLine.enabled = false;
        }

        private void UpdateTetherVisibility(bool visible)
        {
            if (m_TetherLine != null)
            {
                m_TetherLine.enabled = visible;
            }
        }

        private void UpdateTetherAnimation()
        {
            if (!m_IsHoldingHands.Value || m_PartnerTransform == null)
            {
                if (m_TetherLine != null && m_TetherLine.enabled)
                {
                    m_TetherLine.enabled = false;
                }
                ResetVisualLean();
                return;
            }

            if (m_TetherLine != null && !m_TetherLine.enabled)
            {
                m_TetherLine.enabled = true;
            }

            // Tìm điểm gắn tay (Hand Anchor)
            Vector3 myAnchor = GetHandAnchor(transform, isPartner: false);
            Vector3 partnerAnchor = GetHandAnchor(m_PartnerTransform, isPartner: true);

            // Vẽ đường cong Bezier/Catenary uốn lượn mềm mại giữa 2 bàn tay
            int count = 14;
            Vector3 midPoint = (myAnchor + partnerAnchor) * 0.5f;

            // Độ võng tự nhiên của dây + nhịp thở nhẹ nhàng
            float sag = Mathf.Sin(Time.time * 3f) * 0.04f - 0.12f;
            midPoint.y += sag;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                // Quadratic Bezier
                Vector3 p0 = Vector3.Lerp(myAnchor, midPoint, t);
                Vector3 p1 = Vector3.Lerp(midPoint, partnerAnchor, t);
                Vector3 curvePoint = Vector3.Lerp(p0, p1, t);

                m_TetherLine.SetPosition(i, curvePoint);
            }

            // Procedural Lean: Nghiêng người nhẹ nhàng về phía đối phương
            ApplyVisualLean();
        }

        private Vector3 GetHandAnchor(Transform root, bool isPartner)
        {
            if (root == null) return transform.position + Vector3.up * 1f;

            // Nếu là Ba: Vị trí tay phải/hông phải
            // Nếu là Con: Vị trí tay trái/hông trái
            bool isDadActor = isPartner ? (m_PartnerHandAbility != null && m_PartnerHandAbility.IsDad) : IsDad;
            Vector3 sideOffset = isDadActor ? (root.right * 0.45f + Vector3.up * 0.95f) : (-root.right * 0.35f + Vector3.up * 0.65f);

            return root.position + sideOffset;
        }

        private void FindVisualModel()
        {
            if (m_RoleVisuals != null)
            {
                var targetModel = IsDad ? m_RoleVisuals.dadModel : m_RoleVisuals.childModel;
                if (targetModel != null) m_VisualModel = targetModel.transform;
            }

            if (m_VisualModel == null)
            {
                var anim = GetComponentInChildren<Animator>();
                if (anim != null) m_VisualModel = anim.transform;
                else if (transform.childCount > 0) m_VisualModel = transform.GetChild(0);
            }

            if (m_VisualModel != null)
            {
                m_DefaultModelRot = m_VisualModel.localRotation;
            }
        }

        private void ApplyVisualLean()
        {
            if (m_VisualModel == null || m_PartnerTransform == null) return;

            // Tính hướng nghiêng người về phía partner
            Vector3 toPartner = (m_PartnerTransform.position - transform.position).normalized;
            float dotRight = Vector3.Dot(transform.right, toPartner);

            // Nghiêng góc nhỏ 5-8 độ sang bên có partner
            float rollAngle = dotRight * 6.5f;
            m_VisualModel.localRotation = Quaternion.Euler(0f, 0f, -rollAngle) * m_DefaultModelRot;
        }

        private void ResetVisualLean()
        {
            if (m_VisualModel != null)
            {
                m_VisualModel.localRotation = m_DefaultModelRot;
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
                Debug.Log($"[HoldMyHand] 🤝 {(IsDad ? "Ba" : "Con")} đã nắm chặt tay {(IsDad ? "Con" : "Ba")}!");
            }
            else
            {
                ResetVisualLean();
                Debug.Log($"[HoldMyHand] 💨 Đã buông tay.");
            }
        }

        private void OnHoldingStateChanged(bool previous, bool current)
        {
            UpdateTetherVisibility(current);
            if (!current) ResetVisualLean();
        }

        private void FindPartnerIfNeeded()
        {
            if (m_PartnerTransform != null) return;

            // 1. Quét tìm Dummy Companion trước
            var dummy = FindAnyObjectByType<SoloCompanionDummy>();
            if (dummy != null && dummy.gameObject != this.gameObject)
            {
                m_PartnerTransform = dummy.transform;
                m_PartnerHandAbility = dummy.GetComponent<CoopHoldHandAbility>();
                return;
            }

            // 2. Quét qua AllPlayers
            foreach (var player in PlayerRoleVisuals.AllPlayers)
            {
                if (player != null && player.gameObject != this.gameObject)
                {
                    m_PartnerTransform = player.transform;
                    m_PartnerHandAbility = player.GetComponent<CoopHoldHandAbility>();
                    return;
                }
            }

            // 3. Fallback: Quét các component CoopHoldHandAbility trong Scene
            var allAbilities = FindObjectsByType<CoopHoldHandAbility>(FindObjectsSortMode.None);
            foreach (var ability in allAbilities)
            {
                if (ability != null && ability.gameObject != this.gameObject)
                {
                    m_PartnerTransform = ability.transform;
                    m_PartnerHandAbility = ability;
                    return;
                }
            }
        }

        #endregion

        #region UI Prompt

        private void OnGUI()
        {
            // CHỈ NGƯỜI CHƠI THẬT MỚI VẼ GIAO DIỆN NẮM TAY
            if (!IsLocalPlayerCharacter) return;

            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            boxStyle.normal.textColor = Color.white;

            // Cảnh báo tuột tay
            if (!string.IsNullOrEmpty(m_BreakAlert) && m_BreakAlertTimer > 0f)
            {
                GUI.color = new Color(1f, 0.25f, 0.25f, 0.95f);
                GUI.Box(new Rect(Screen.width / 2f - 240, Screen.height - 110, 480, 42), m_BreakAlert, boxStyle);
                return;
            }

            // Gợi ý bấm [H] để nắm tay
            if (m_IsPartnerNearby && !m_IsHoldingHands.Value)
            {
                string partnerName = IsDad ? "Con (👶)" : "Ba (🧔)";
                GUI.color = new Color(1f, 0.85f, 0.2f, 0.95f);
                GUI.Box(new Rect(Screen.width / 2f - 200, Screen.height - 110, 400, 42), 
                    $"🤝 Bấm [H] để Nắm Tay {partnerName}", boxStyle);
            }
            else if (m_IsHoldingHands.Value)
            {
                GUI.color = new Color(0.2f, 0.92f, 1f, 0.95f);
                GUI.Box(new Rect(Screen.width / 2f - 220, Screen.height - 110, 440, 42), 
                    $"🤝 Đang Nắm Tay! Cùng Chạy & Vượt Chướng Ngại (Bấm [H] Buông)", boxStyle);
            }
        }

        #endregion
    }
}
