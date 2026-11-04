using System;
using System.Collections;
using UnityEngine;
using Unity.Netcode;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Cơ chế Kiệu Vai & Gồng Lực Ném Con Lên Cao (Charged Boost & Throw Ability):
    /// - 🧔 Ba (Dad): Nhấn giữ [F] để gồng cơ bắp tích lũy lực ném ("thanh canh lực"),
    ///   hiển thị đường đạn parabolic dự đoán (Trajectory Arc) và hiệu ứng squash & shake cơ bắp.
    ///   Thả [F] ra để quăng 👶 Con phóng lên vách cao hoặc qua khe vực sâu theo quán tính.
    /// - 👶 Con (Child): Khi bay trên không kích hoạt tư thế phóng lượn (JumpInAir_Launched),
    ///   bay xa theo quán tính phương ngang và tiếp đất an toàn.
    /// </summary>
    [RequireComponent(typeof(CoreMovement))]
    public class CoopBoostThrowAbility : NetworkBehaviour
    {
        #region Serialized Fields

        [Header("Thông số Lực Phóng (Force Range)")]
        [Tooltip("Lực phóng thẳng đứng tối thiểu khi vừa nhấn (mét/giây)")]
        [SerializeField] private float minVerticalBoost = 7.0f;

        [Tooltip("Lực phóng thẳng đứng cực đại khi gồng đầy thanh lực (mét/giây)")]
        [SerializeField] private float maxVerticalBoost = 16.5f;

        [Tooltip("Lực đẩy phương ngang tối thiểu (mét/giây)")]
        [SerializeField] private float minForwardBoostForce = 5.5f;

        [Tooltip("Lực đẩy phương ngang cực đại khi gồng đầy (mét/giây)")]
        [SerializeField] private float maxForwardBoostForce = 15.0f;

#pragma warning disable CS0414
        [Header("Thông số Tích Lực (Charge Tuning)")]
        [Tooltip("Thời gian cần giữ [F] để tích đủ 100% lực (giây)")]
        [SerializeField] private float maxChargeDuration = 1.25f;

        [Tooltip("Thời gian nhấn giữ tối thiểu để tính là ném (tránh chạm nhầm phím)")]
        [SerializeField] private float minHoldThreshold = 0.12f;

        [Tooltip("Khoảng cách tối đa để bắt đầu gồng ném (mét)")]
        [SerializeField] private float maxBoostDistance = 3.2f;

        [Tooltip("Thời gian hồi giữa các lần phóng (giây)")]
        [SerializeField] private float boostCooldown = 1.2f;

        [Tooltip("Tiêu hao thể lực tối thiểu")]
        [SerializeField] private float dadStaminaCostMin = 8.0f;

        [Tooltip("Tiêu hao thể lực cực đại khi gồng full 100%")]
        [SerializeField] private float dadStaminaCostMax = 22.0f;

        [Header("Phím bấm")]
        [Tooltip("Phím thực hiện kiệu phóng")]
        [SerializeField] private KeyCode boostKey = KeyCode.F;
#pragma warning restore CS0414

        #endregion

        #region Private Fields

        private CoreMovement m_Motor;
        private PlayerRoleVisuals m_RoleVisuals;
        private CoreStatsHandler m_StatsHandler;
        private Animator m_Animator;
        private CharacterController m_CharacterController;
        private CoreCameraController m_CameraController;

        // Visual Model Transform for procedural squash & stretch
        private Transform m_VisualModel;
        private Vector3 m_OriginalModelScale = Vector3.one;
        private Vector3 m_OriginalModelPos = Vector3.zero;
        private Quaternion m_OriginalModelRot = Quaternion.identity;

        // Trajectory Line Renderer
        private LineRenderer m_TrajectoryLine;
        private GameObject m_LandingMarker;

        // Cooldown & Partner Tracking
        private float m_CooldownTimer = 0f;
        private Transform m_PartnerTransform;
        private CoopBoostThrowAbility m_PartnerBoostAbility;
        private bool m_CanBoostPartner = false;

        // Charging State
        private bool m_IsCharging = false;
        private float m_CurrentChargeTime = 0f;
        private float m_CurrentChargeRatio = 0f; // 0.0 -> 1.0

        // Active Flight Coroutine
        private Coroutine m_ActiveFlightCoroutine;

        // Textures for GUI Force Bar
        private Texture2D m_BarBgTexture;
        private Texture2D m_BarFillTexture;

        // Animator Hashes
        private static readonly int s_JumpStartHeavyHash = Animator.StringToHash("JumpStartHeavy");
        private static readonly int s_JumpStartHeavyCompleteHash = Animator.StringToHash("JumpStartHeavyComplete");
        private static readonly int s_JumpLandHeavyHash = Animator.StringToHash("JumpLandHeavy");
        private static readonly int s_IsPiggybackingHash = Animator.StringToHash("IsPiggybacking");

        // Piggyback Mounting State (Con ngồi cõng trên lưng Ba)
        private bool m_IsPiggybackingOnDad = false;
        private Transform m_DadMountTransform = null;

        #endregion

        #region Properties

        /// <summary>
        /// Kiểm tra xem instance này có phải nhân vật do NGƯỜI CHƠI THẬT ĐIỀU KHIỂN không (loại trừ Dummy Companion).
        /// </summary>
        public bool IsLocalPlayerCharacter
        {
            get
            {
                // Nếu gắn component SoloCompanionDummy -> chắc chắn không phải người chơi
                if (GetComponent<SoloCompanionDummy>() != null) return false;

                // Nếu có NetworkManager -> kiểm tra PlayerObject được chỉ định
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

        private NetworkVariable<bool> m_IsDadNetworkVar = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        [Header("Vai trò")]
        [Tooltip("Đánh dấu đây có phải là Ba hay không")]
        [SerializeField] private bool isDadRole = true;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsOwner)
            {
                bool isDad = DetermineIsDad();
                isDadRole = isDad;
                try
                {
                    m_IsDadNetworkVar.Value = isDad;
                }
                catch (System.Exception) { }
            }
        }

        /// <summary>
        /// Gán vai trò Ba (true) hoặc Con (false) từ Server.
        /// </summary>
        public void SetRole(bool isDad)
        {
            isDadRole = isDad;
            if (IsSpawned)
            {
                bool canWrite = IsOwner || (IsServer && NetworkManager.Singleton != null && !NetworkManager.Singleton.DistributedAuthorityMode);
                if (canWrite)
                {
                    try
                    {
                        m_IsDadNetworkVar.Value = isDad;
                    }
                    catch (System.Exception)
                    {
                        // Bỏ qua ngoại lệ phân quyền Netcode nếu có
                    }
                }
            }
        }

        private bool DetermineIsDad()
        {
            string objName = gameObject.name.ToLower();
            if (objName.Contains("child") || objName.Contains("son") || objName.Contains("con")) return false;
            if (objName.Contains("dad") || objName.Contains("father") || objName.Contains("ba")) return true;

            if (m_CharacterController != null && m_CharacterController.height < 1.45f) return false;

            if (IsLocalPlayerCharacter)
            {
                return Lobby3DManager.AmIDad;
            }

            if (m_RoleVisuals != null)
            {
                return m_RoleVisuals.IsDad;
            }

            return isDadRole;
        }

        public bool IsDad
        {
            get
            {
                // 1. Kiểm tra dứt khoát theo tên GameObject
                string objName = gameObject.name.ToLower();
                if (objName.Contains("child") || objName.Contains("son") || objName.Contains("con")) return false;
                if (objName.Contains("dad") || objName.Contains("father") || objName.Contains("ba")) return true;

                // 2. Chiều cao CharacterController của con luôn thấp hơn cha đáng kể (< 1.45m)
                if (m_CharacterController != null && m_CharacterController.height < 1.45f) return false;

                // 3. Nếu là nhân vật do chính người chơi trên máy này điều khiển -> dùng Lobby3DManager.AmIDad
                if (IsLocalPlayerCharacter)
                {
                    return Lobby3DManager.AmIDad;
                }

                // 4. Kiểm tra PlayerRoleVisuals nếu có
                if (m_RoleVisuals != null) return m_RoleVisuals.IsDad;

                // 5. Kiểm tra biến mạng Netcode nếu đã Spawn
                if (IsSpawned) return m_IsDadNetworkVar.Value;

                return isDadRole;
            }
        }

        public bool IsCharging => m_IsCharging;
        public float CurrentChargeRatio => m_CurrentChargeRatio;
        // Authored launch pads can supply a landing marker; ordinary throws retain mouse aiming.
        public Transform AssistedLandingTarget { get; set; }

        private void AssistedLaunch(Vector3 start, ref Vector3 direction, ref float vertical, ref float horizontal)
        {
            if (AssistedLandingTarget == null) return;
            Vector3 delta = AssistedLandingTarget.position - start;
            float gravityMagnitude = m_PartnerBoostAbility != null && m_PartnerBoostAbility.m_Motor != null
                ? Mathf.Abs(m_PartnerBoostAbility.m_Motor.gravity) : 15f;
            gravityMagnitude = Mathf.Max(1f, gravityMagnitude);
            vertical = Mathf.Sqrt(2f * gravityMagnitude * (Mathf.Max(0f, delta.y) + 2.5f));
            float duration = (vertical + Mathf.Sqrt(Mathf.Max(0f, vertical * vertical - 2f * gravityMagnitude * delta.y))) / gravityMagnitude;
            Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
            direction = flat.normalized;
            horizontal = flat.magnitude / Mathf.Max(0.1f, duration - 0.02f * duration * duration);
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            m_Motor = GetComponent<CoreMovement>();
            m_RoleVisuals = GetComponent<PlayerRoleVisuals>();
            m_StatsHandler = GetComponent<CoreStatsHandler>();
            m_Animator = GetComponentInChildren<Animator>();
            m_CharacterController = GetComponent<CharacterController>();
            m_CameraController = GetComponent<CoreCameraController>() ?? GetComponentInParent<CoreCameraController>();

            // Tự động nhận diện role từ tên GameObject ngay khi Awake
            string objName = gameObject.name.ToLower();
            if (objName.Contains("child") || objName.Contains("son")) isDadRole = false;
            else if (objName.Contains("dad")) isDadRole = true;

            FindVisualModel();
            SetupTrajectoryLine();
            CreateGUIStyles();
        }

        private void Update()
        {
            if (m_CooldownTimer > 0f)
            {
                m_CooldownTimer -= Time.deltaTime;
            }

            // CHỈ NGƯỜI CHƠI THẬT MỚI XỬ LÝ INPUT BÀN PHÍM VÀ GỒNG NÉM
            if (!IsLocalPlayerCharacter) return;

            // CHỈ BA (DAD) MỚI CÓ THỂ GỒNG LỰC NÉM CON - CON TUYỆT ĐỐI KHÔNG ĐƯỢC NÉM BA
            if (!IsDad)
            {
                if (m_IsCharging) CancelCharge();
                return;
            }

            FindPartnerIfNeeded();

            if (m_PartnerTransform == null)
            {
                m_CanBoostPartner = false;
                if (m_IsCharging) CancelCharge();
                return;
            }

            float dist = Vector3.Distance(transform.position, m_PartnerTransform.position);
            m_CanBoostPartner = dist <= maxBoostDistance && m_CooldownTimer <= 0f;

            // Nhận diện Input New Input System & Legacy
            bool fPressedThisFrame = false;
            bool fHeld = false;
            bool fReleasedThisFrame = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                fPressedThisFrame = Keyboard.current.fKey.wasPressedThisFrame;
                fHeld = Keyboard.current.fKey.isPressed;
                fReleasedThisFrame = Keyboard.current.fKey.wasReleasedThisFrame;
            }
#else
            fPressedThisFrame = Input.GetKeyDown(boostKey) || Input.GetKeyDown(KeyCode.F);
            fHeld = Input.GetKey(boostKey) || Input.GetKey(KeyCode.F);
            fReleasedThisFrame = Input.GetKeyUp(boostKey) || Input.GetKeyUp(KeyCode.F);
#endif

            // Xử lý logic gồng và ném theo hướng nhìn chuột (Chỉ Ba mới được ném Con)
            HandleCoopBoostCharging(fPressedThisFrame, fHeld, fReleasedThisFrame, dist);
        }

        private void LateUpdate()
        {
            // Nếu Con đang ngồi cõng trên vai phải Ba -> giữ vị trí và góc quay dính chặt vào Socket vai phải Ba
            if (m_IsPiggybackingOnDad && m_DadMountTransform != null)
            {
                Vector3 socketPos = m_DadMountTransform.position + m_DadMountTransform.rotation * new Vector3(0.25f, 1.08f, 0.02f);
                Quaternion socketRot = m_DadMountTransform.rotation * Quaternion.Euler(4f, 0f, 0f);
                transform.position = Vector3.Lerp(transform.position, socketPos, Time.deltaTime * 24f);
                transform.rotation = Quaternion.Slerp(transform.rotation, socketRot, Time.deltaTime * 24f);
            }
        }

        public void CancelForRespawn()
        {
            if (m_ActiveFlightCoroutine != null)
            {
                StopCoroutine(m_ActiveFlightCoroutine);
                m_ActiveFlightCoroutine = null;
            }
            if (m_IsCharging) CancelCharge();
            if (m_IsPiggybackingOnDad) SetPiggybackStateLocally(false, null);
            if (m_VisualModel != null)
            {
                m_VisualModel.localPosition = m_OriginalModelPos;
                m_VisualModel.localRotation = m_OriginalModelRot;
                m_VisualModel.localScale = m_OriginalModelScale;
            }
            if (m_Motor != null) m_Motor.ResetMovementForces();
        }

        private void OnDisable()
        {
            if (m_IsCharging) CancelCharge();
            if (m_IsPiggybackingOnDad) SetPiggybackStateLocally(false, null);
            if (m_TrajectoryLine != null) m_TrajectoryLine.enabled = false;
            if (m_LandingMarker != null) m_LandingMarker.SetActive(false);
        }

        #endregion

        #region Co-op Boost Charging & Execution (Dad Throwing Child Only)

        private void HandleCoopBoostCharging(bool fPressedThisFrame, bool fHeld, bool fReleasedThisFrame, float partnerDistance)
        {
            // 0. CHỈ BA (DAD) MỚI ĐƯỢC GỒNG NÉM CON - CON TUYỆT ĐỐI KHÔNG ĐƯỢC NÉM BA
            if (!IsDad)
            {
                if (m_IsCharging) CancelCharge();
                return;
            }

            // 1. BẮT ĐẦU GỒNG (F Pressed)
            if (fPressedThisFrame && m_CanBoostPartner && !m_IsCharging)
            {
                StartCharging();
            }

            // 2. ĐANG GỒNG TÍCH LỰC (F Held)
            if (m_IsCharging)
            {
                // Hủy nếu partner đi quá xa
                if (partnerDistance > maxBoostDistance * 1.8f)
                {
                    CancelCharge();
                    return;
                }

                m_CurrentChargeTime += Time.deltaTime;
                m_CurrentChargeRatio = Mathf.Clamp01(m_CurrentChargeTime / maxChargeDuration);

                // Tính toán hướng ngắm chuột (Mouse Look / Camera Forward)
                Vector3 camFwd = Camera.main != null ? Camera.main.transform.forward : transform.forward;
                Vector3 flatAimDir = Vector3.ProjectOnPlane(camFwd, Vector3.up).normalized;
                if (flatAimDir.sqrMagnitude > 0.01f)
                {
                    // Ba xoay người mượt mà theo hướng ngắm của chuột
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flatAimDir), Time.deltaTime * 14f);
                }

                // Con ngồi ngay ngắn trên vai phải Ba (Right Shoulder Perch Socket)
                if (m_PartnerTransform != null)
                {
                    Vector3 socketPos = transform.position + transform.rotation * new Vector3(0.25f, 1.08f, 0.02f);
                    Quaternion socketRot = transform.rotation * Quaternion.Euler(4f, 0f, 0f);
                    m_PartnerTransform.position = Vector3.Lerp(m_PartnerTransform.position, socketPos, Time.deltaTime * 24f);
                    m_PartnerTransform.rotation = Quaternion.Slerp(m_PartnerTransform.rotation, socketRot, Time.deltaTime * 24f);
                }

                // Procedural Animation: Gồng cơ bắp (Squash Y & Tremble Shake)
                ApplySquashAndTremble(m_CurrentChargeRatio);

                // Cập nhật đường đạn dự đoán Parabolic Arc theo hướng chuột
                UpdateTrajectoryArc(m_CurrentChargeRatio, flatAimDir, camFwd);

                // 3. THẢ F -> QUĂNG CON THEO HƯỚNG CHUỘT!
                if (fReleasedThisFrame)
                {
                    float effectiveRatio = Mathf.Max(0.15f, m_CurrentChargeRatio);
                    ExecuteDadThrow(effectiveRatio, flatAimDir, camFwd);
                }
            }
        }

        private void StartCharging()
        {
            m_IsCharging = true;
            m_CurrentChargeTime = 0f;
            m_CurrentChargeRatio = 0f;

            // Kích hoạt Con leo lên lưng ngồi cõng (Piggyback Socket)
            SetPartnerPiggyback(true);

            // Kích hoạt camera phóng to lệch vai phải OTS để ngắm ném mượt mà
            if (m_CameraController != null)
            {
                m_CameraController.SetAimZoom(true);
            }

            if (m_TrajectoryLine != null) m_TrajectoryLine.enabled = true;
            if (m_LandingMarker != null) m_LandingMarker.SetActive(true);

            UISoundManager.PlayClick();
            Debug.Log("[CoopBoost] 🧔 Ba bắt đầu gồng cơ bắp tích lực, cõng con trên lưng và ngắm ném qua OTS camera...");
        }

        private void ApplySquashAndTremble(float ratio)
        {
            if (m_VisualModel == null) return;

            // Gập người xuống (Squash Y: 1.0 -> 0.82) và bành vai ngang (Stretch XZ: 1.0 -> 1.10)
            float squashY = Mathf.Lerp(1.0f, 0.82f, ratio);
            float stretchXZ = Mathf.Lerp(1.0f, 1.10f, ratio);
            m_VisualModel.localScale = new Vector3(
                m_OriginalModelScale.x * stretchXZ,
                m_OriginalModelScale.y * squashY,
                m_OriginalModelScale.z * stretchXZ
            );

            // Rung lắc cơ bắp tỷ lệ với độ căng cơ (Muscle Tremble)
            float trembleIntensity = Mathf.Lerp(0.005f, 0.025f, ratio);
            Vector3 jitter = UnityEngine.Random.insideUnitSphere * trembleIntensity;
            jitter.y *= 0.5f;
            m_VisualModel.localPosition = m_OriginalModelPos + jitter;
        }

        private void ExecuteDadThrow(float ratio, Vector3 horizontalDir, Vector3 camForward)
        {
            // CHỈ BA (DAD) MỚI CÓ THỂ NÉM CON - BẢO VỆ TUYỆT ĐỐI
            if (!IsDad)
            {
                Debug.LogError("[CoopBoost] ❌ Lỗi: Con tuyệt đối không thể thực hiện ExecuteDadThrow!");
                CancelCharge();
                return;
            }

            // Điều khiển NÉM CAO (di chuột ngước lên) hoặc NÉM XA (di chuột ngang/chúi xuống)
            float camPitchY = Mathf.Clamp(camForward.y, -0.35f, 0.65f);
            float pitchFactor = Mathf.InverseLerp(-0.35f, 0.65f, camPitchY); // 0.0 (chúi/ngang) -> 1.0 (ngước lên)

            // Ngước lên: tăng mạnh vận tốc đứng lên tới ~28m/s để ném con vọt lên đỉnh vách đá
            // Nhìn ngang/chúi: tăng cực mạnh lực đẩy ngang lên tới ~25m/s để ném con bay xa qua khe vực
            float verticalMultiplier = Mathf.Lerp(0.55f, 1.85f, pitchFactor);
            float forwardMultiplier = Mathf.Lerp(1.75f, 0.50f, pitchFactor);

            float baseVertical = Mathf.Lerp(minVerticalBoost, maxVerticalBoost, ratio);
            float baseForward = Mathf.Lerp(minForwardBoostForce, maxForwardBoostForce, ratio);

            float finalVerticalVel = baseVertical * verticalMultiplier;
            float finalForwardForce = baseForward * forwardMultiplier;
            float staminaCost = Mathf.Lerp(dadStaminaCostMin, dadStaminaCostMax, ratio);

            // Kiểm tra và tiêu hao thể lực của Ba
            if (m_StatsHandler != null)
            {
                m_StatsHandler.TryConsumeStat(Animator.StringToHash("Stamina"), staminaCost);
            }

            Vector3 launchDirection = horizontalDir.sqrMagnitude > 0.01f ? horizontalDir : transform.forward;
            AssistedLaunch(transform.position + transform.right * 0.65f, ref launchDirection, ref finalVerticalVel, ref finalForwardForce);

            // 0. Tháo Con khỏi lưng trước khi phóng bay
            SetPartnerPiggyback(false);

            // Gửi lực phóng tới Con
            if (m_PartnerBoostAbility != null)
            {
                if (m_PartnerBoostAbility.IsSpawned && !m_PartnerBoostAbility.IsLocalPlayerCharacter)
                {
                    m_PartnerBoostAbility.ReceiveBoostLaunchRpc(launchDirection, finalVerticalVel, finalForwardForce);
                }
                else
                {
                    m_PartnerBoostAbility.ApplyLaunchLocally(launchDirection, finalVerticalVel, finalForwardForce);
                }
            }
            else if (m_PartnerTransform != null)
            {
                var partnerAbility = m_PartnerTransform.GetComponent<CoopBoostThrowAbility>();
                if (partnerAbility != null)
                {
                    partnerAbility.ApplyLaunchLocally(launchDirection, finalVerticalVel, finalForwardForce);
                }
            }

            // Trả camera về góc nhìn mượt mà bình thường
            if (m_CameraController != null)
            {
                m_CameraController.SetAimZoom(false);
            }

            // Đảm bảo Animator của Ba ở lại locomotion (Idle Walk Run), không bị kẹt vào InAir
            if (m_Animator != null)
            {
                m_Animator.ResetTrigger(s_JumpStartHeavyHash);
                m_Animator.ResetTrigger(s_JumpStartHeavyCompleteHash);
                m_Animator.ResetTrigger(s_JumpLandHeavyHash);
                m_Animator.CrossFadeInFixedTime("Idle Walk Run Blend", 0.15f);
            }

            // Hiệu ứng hồi bật co giãn (Elastic Rebound Bounce)
            StartCoroutine(RoutineReboundStretch());

            // Đặt hồi chiêu
            m_CooldownTimer = boostCooldown;
            m_IsCharging = false;
            m_CurrentChargeTime = 0f;
            m_CurrentChargeRatio = 0f;

            if (m_TrajectoryLine != null) m_TrajectoryLine.enabled = false;
            if (m_LandingMarker != null) m_LandingMarker.SetActive(false);

            UISoundManager.PlayClick();
            Debug.Log($"[CoopBoost] 🔥 🧔 Ba QUĂNG CON THEO HƯỚNG CHUỘT! Lực: {(int)(ratio * 100)}% (Dir={launchDirection}, V_Vel={finalVerticalVel:F1}m/s, Fwd={finalForwardForce:F1})");
        }

        private void CancelCharge()
        {
            m_IsCharging = false;
            m_CurrentChargeTime = 0f;
            m_CurrentChargeRatio = 0f;

            // Trả góc nhìn camera về bình thường mượt mà
            if (m_CameraController != null)
            {
                m_CameraController.SetAimZoom(false);
            }

            if (m_VisualModel != null)
            {
                m_VisualModel.localScale = m_OriginalModelScale;
                m_VisualModel.localPosition = m_OriginalModelPos;
            }

            if (m_Animator != null)
            {
                m_Animator.ResetTrigger(s_JumpStartHeavyHash);
                m_Animator.ResetTrigger(s_JumpStartHeavyCompleteHash);
                m_Animator.ResetTrigger(s_JumpLandHeavyHash);
                m_Animator.CrossFadeInFixedTime("Idle Walk Run Blend", 0.15f);
            }

            if (m_TrajectoryLine != null) m_TrajectoryLine.enabled = false;
            if (m_LandingMarker != null) m_LandingMarker.SetActive(false);

            // Cho Con xuống đất an toàn phía sau Ba
            SetPartnerPiggyback(false);
        }

        private void SetPartnerPiggyback(bool isMounted)
        {
            if (m_PartnerBoostAbility != null)
            {
                // Cập nhật visual ngay lập tức trên máy cục bộ
                m_PartnerBoostAbility.SetPiggybackStateLocally(isMounted, transform);

                // Gửi RPC đồng bộ sang máy Client nếu là mạng nhiều người
                if (m_PartnerBoostAbility.IsSpawned && !m_PartnerBoostAbility.IsLocalPlayerCharacter)
                {
                    ulong myNetId = NetworkObject != null ? NetworkObject.NetworkObjectId : 0;
                    m_PartnerBoostAbility.SetPiggybackMountRpc(isMounted, myNetId);
                }
            }
            else if (m_PartnerTransform != null)
            {
                var partnerAbility = m_PartnerTransform.GetComponent<CoopBoostThrowAbility>();
                if (partnerAbility != null)
                {
                    partnerAbility.SetPiggybackStateLocally(isMounted, transform);
                }
            }
        }

        private IEnumerator RoutineReboundStretch()
        {
            if (m_VisualModel == null) yield break;

            float t = 0f;
            float duration = 0.32f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float progress = t / duration;

                float bounce = Mathf.Sin(progress * Mathf.PI * 2.5f) * Mathf.Exp(-progress * 4.5f);
                float scaleY = 1.0f + bounce * 0.22f;
                float scaleXZ = 1.0f - bounce * 0.10f;

                m_VisualModel.localScale = new Vector3(
                    m_OriginalModelScale.x * scaleXZ,
                    m_OriginalModelScale.y * scaleY,
                    m_OriginalModelScale.z * scaleXZ
                );
                yield return null;
            }

            m_VisualModel.localScale = m_OriginalModelScale;
            m_VisualModel.localPosition = m_OriginalModelPos;
        }

        #endregion

        #region Child Receive Launch & Local Handling

        [Rpc(SendTo.Owner)]
        public void SetPiggybackMountRpc(bool isMounted, ulong dadNetworkObjectId)
        {
            if (IsDad)
            {
                Debug.LogWarning("[CoopBoost] ⛔ Ba không thể ngồi piggyback trên người khác!");
                return;
            }

            Transform dadTr = null;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(dadNetworkObjectId, out var netObj))
            {
                dadTr = netObj.transform;
            }
            SetPiggybackStateLocally(isMounted, dadTr);
        }

        /// <summary>
        /// Kích hoạt tư thế ngồi trên lưng Ba (Piggyback) hoặc tháo xuống.
        /// </summary>
        public void SetPiggybackStateLocally(bool isMounted, Transform dadTransform)
        {
            m_IsPiggybackingOnDad = isMounted;
            m_DadMountTransform = isMounted ? dadTransform : null;

            if (m_CharacterController == null) m_CharacterController = GetComponent<CharacterController>();
            if (m_Motor == null) m_Motor = GetComponent<CoreMovement>();

            var childAnim = GetComponentInChildren<Animator>();

            if (isMounted)
            {
                if (m_CharacterController != null) m_CharacterController.enabled = false;
                if (m_Motor != null) m_Motor.SetSittingState(false);
                if (childAnim != null)
                {
                    childAnim.SetBool(s_IsPiggybackingHash, true);
                    childAnim.CrossFadeInFixedTime("Piggyback_Sit", 0.12f);
                }
                if (dadTransform != null)
                {
                    Vector3 socketPos = dadTransform.position + dadTransform.rotation * new Vector3(0.25f, 1.08f, 0.02f);
                    transform.position = socketPos;
                    transform.rotation = dadTransform.rotation * Quaternion.Euler(4f, 0f, 0f);
                }
            }
            else
            {
                if (childAnim != null)
                {
                    childAnim.SetBool(s_IsPiggybackingHash, false);
                    if (m_ActiveFlightCoroutine == null)
                    {
                        childAnim.CrossFadeInFixedTime("Idle Walk Run Blend", 0.15f);
                    }
                }
                // Nếu không đang bay ballistic, bật lại CharacterController và trả về mặt đất
                if (m_ActiveFlightCoroutine == null)
                {
                    if (m_CharacterController != null) m_CharacterController.enabled = true;
                    if (dadTransform != null)
                    {
                        // Đặt con xuống đất an toàn cạnh bên phải ba
                        transform.position = dadTransform.position + dadTransform.right * 0.65f;
                    }
                }
            }
        }

        [Rpc(SendTo.Owner)]
        public void ReceiveBoostLaunchRpc(Vector3 forwardDirection, float verticalVelocity, float forwardForce)
        {
            // BẢO VỆ TUYỆT ĐỐI: CHỈ CON MỚI ĐƯỢC NHẬN LỰC PHÓNG BAY - BA TUYỆT ĐỐI KHÔNG BỊ PHÓNG
            if (IsDad)
            {
                Debug.LogWarning("[CoopBoost] ⛔ Chặn nhận lực ném: Ba (Dad) tuyệt đối không thể bị ném!");
                return;
            }

            ApplyLaunchLocally(forwardDirection, verticalVelocity, forwardForce);
        }

        /// <summary>
        /// Kích hoạt cơ chế bay xa theo quán tính phương ngang và bổng lên theo phương đứng.
        /// </summary>
        public void ApplyLaunchLocally(Vector3 forwardDirection, float verticalVelocity, float forwardForce)
        {
            // BẢO VỆ TUYỆT ĐỐI: CHỈ CON MỚI ĐƯỢC PHÓNG BAY
            if (IsDad)
            {
                Debug.LogWarning("[CoopBoost] ⛔ Ba (Dad) tuyệt đối không thể bị phóng bay!");
                return;
            }

            if (m_Motor == null) m_Motor = GetComponent<CoreMovement>();
            if (m_CharacterController == null) m_CharacterController = GetComponent<CharacterController>();

            if (m_ActiveFlightCoroutine != null)
            {
                StopCoroutine(m_ActiveFlightCoroutine);
            }

            m_ActiveFlightCoroutine = StartCoroutine(RoutineBallisticFlight(forwardDirection, forwardForce, verticalVelocity));
            UISoundManager.PlayClick();
        }

        /// <summary>
        /// Coroutine duy trì quán tính bay (Inertia Momentum) theo phương ngang xuyên suốt quá trình trên không.
        /// </summary>
        private IEnumerator RoutineBallisticFlight(Vector3 forwardDirection, float forwardSpeed, float verticalVelocity)
        {
            if (m_Motor == null) yield break;

            // 1. Kích hoạt vận tốc phóng thẳng đứng trên CoreMovement
            m_Motor.SetVerticalVelocity(verticalVelocity);

            // 2. Định hướng nhân vật quay mặt về hướng ném
            Vector3 flatForward = new Vector3(forwardDirection.x, 0f, forwardDirection.z).normalized;
            if (flatForward.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(flatForward);
            }

            // 3. Trigger Animation Con phóng lượn trên không (JumpInAir_Launched)
            var childAnim = GetComponentInChildren<Animator>();
            if (childAnim != null)
            {
                childAnim.SetTrigger(s_JumpStartHeavyCompleteHash);
            }

            float airTimer = 0f;
            float maxFlightTime = 3.5f;

            // Đợi 1 frame để nhân vật rời khỏi mặt đất
            yield return null;

            // 4. DUY TRÌ QUÁN TÍNH PHƯƠNG NGANG CHO ĐẾN KHI CHẠM ĐẤT
            while (airTimer < maxFlightTime)
            {
                airTimer += Time.deltaTime;

                // Kiểm tra tiếp đất sau ít nhất 0.2s bay
                if (airTimer > 0.2f && (m_Motor.IsGrounded || (m_CharacterController != null && m_CharacterController.isGrounded)))
                {
                    break;
                }

                // Vận tốc ngang duy trì theo quán tính (chỉ giảm rất nhẹ 4% mỗi giây do sức cản không khí)
                float currentHorizSpeed = forwardSpeed * Mathf.Clamp01(1.0f - (airTimer * 0.04f));
                Vector3 displacement = flatForward * (currentHorizSpeed * Time.deltaTime);

                if (m_CharacterController != null && m_CharacterController.enabled)
                {
                    m_CharacterController.Move(displacement);
                }
                else
                {
                    transform.position += displacement;
                }

                // Procedural Animation: Nghiêng người về phía trước theo quán tính bay
                if (m_VisualModel != null)
                {
                    float tiltAngle = Mathf.Sin(Mathf.Clamp01(airTimer / 0.8f) * Mathf.PI) * 16f;
                    m_VisualModel.localRotation = Quaternion.Euler(tiltAngle, 0f, 0f) * m_OriginalModelRot;
                }

                yield return null;
            }

            // 5. TIẾP ĐẤT AN TOÀN
            if (m_CharacterController != null)
            {
                m_CharacterController.enabled = true;
            }

            if (m_VisualModel != null)
            {
                m_VisualModel.localRotation = m_OriginalModelRot;
            }

            if (childAnim != null)
            {
                childAnim.SetTrigger(s_JumpLandHeavyHash);
            }

            UISoundManager.PlayClick();
            m_ActiveFlightCoroutine = null;
            Debug.Log("[CoopBoost] 👶 Con đã tiếp đất an toàn sau khi bay xa theo quán tính!");

            yield return new WaitForSeconds(0.3f);
            if (childAnim != null)
            {
                childAnim.ResetTrigger(s_JumpStartHeavyCompleteHash);
                childAnim.ResetTrigger(s_JumpLandHeavyHash);
                childAnim.CrossFadeInFixedTime("Idle Walk Run Blend", 0.15f);
            }
        }

        #endregion

        #region Trajectory Arc & Marker

        private void SetupTrajectoryLine()
        {
            var lineObj = new GameObject("ThrowTrajectoryArc");
            lineObj.transform.SetParent(transform, false);

            m_TrajectoryLine = lineObj.AddComponent<LineRenderer>();
            m_TrajectoryLine.useWorldSpace = true;
            m_TrajectoryLine.positionCount = 30;
            m_TrajectoryLine.startWidth = 0.14f;
            m_TrajectoryLine.endWidth = 0.04f;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = new Color(1f, 0.85f, 0.1f, 0.9f);
                m_TrajectoryLine.material = mat;
            }

            m_TrajectoryLine.startColor = new Color(1f, 0.9f, 0.2f, 0.95f);
            m_TrajectoryLine.endColor = new Color(1f, 0.3f, 0.1f, 0.7f);
            m_TrajectoryLine.enabled = false;

            m_LandingMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            m_LandingMarker.name = "ThrowLandingMarker";
            m_LandingMarker.transform.SetParent(lineObj.transform, false);
            m_LandingMarker.transform.localScale = new Vector3(0.9f, 0.04f, 0.9f);

            var markerCol = m_LandingMarker.GetComponent<Collider>();
            if (markerCol != null) Destroy(markerCol);

            var markerRend = m_LandingMarker.GetComponent<Renderer>();
            if (markerRend != null && shader != null)
            {
                var mat = new Material(shader);
                mat.color = new Color(1f, 0.4f, 0.1f, 0.65f);
                markerRend.material = mat;
            }
            m_LandingMarker.SetActive(false);
        }

        private void UpdateTrajectoryArc(float ratio, Vector3 horizontalDir, Vector3 camForward)
        {
            if (m_TrajectoryLine == null) return;

            float camPitchY = Mathf.Clamp(camForward.y, -0.35f, 0.65f);
            float pitchFactor = Mathf.InverseLerp(-0.35f, 0.65f, camPitchY);

            float verticalMultiplier = Mathf.Lerp(0.55f, 1.85f, pitchFactor);
            float forwardMultiplier = Mathf.Lerp(1.75f, 0.50f, pitchFactor);

            float baseVertical = Mathf.Lerp(minVerticalBoost, maxVerticalBoost, ratio);
            float baseForward = Mathf.Lerp(minForwardBoostForce, maxForwardBoostForce, ratio);

            float vY = baseVertical * verticalMultiplier;
            float fF = baseForward * forwardMultiplier;

            Vector3 forwardDir = horizontalDir.sqrMagnitude > 0.01f ? horizontalDir : transform.forward;
            Vector3 startPos = transform.position + transform.right * 0.25f + forwardDir * 0.5f + Vector3.up * 1.45f;
            if (AssistedLandingTarget != null)
            {
                startPos = transform.position + transform.right * 0.65f;
                AssistedLaunch(startPos, ref forwardDir, ref vY, ref fF);
            }

            Vector3 velocity = forwardDir * fF + Vector3.up * vY;
            Vector3 gravity = Vector3.up * (m_PartnerBoostAbility != null && m_PartnerBoostAbility.m_Motor != null ? m_PartnerBoostAbility.m_Motor.gravity : -15f);

            int count = 30;
            float timeStep = 0.05f;
            Vector3 prevPos = startPos;
            bool hitGround = false;
            Vector3 hitPoint = startPos;

            for (int i = 0; i < count; i++)
            {
                float t = i * timeStep;
                Vector3 currentPos = startPos + velocity * t + 0.5f * gravity * (t * t);

                if (!hitGround && i > 0)
                {
                    if (Physics.Linecast(prevPos, currentPos, out RaycastHit hit, ~LayerMask.GetMask("Ignore Raycast", "Player")))
                    {
                        hitGround = true;
                        hitPoint = hit.point;
                        m_TrajectoryLine.positionCount = i + 1;
                        m_TrajectoryLine.SetPosition(i, hitPoint);
                        break;
                    }
                }

                if (!hitGround)
                {
                    m_TrajectoryLine.positionCount = i + 1;
                    m_TrajectoryLine.SetPosition(i, currentPos);
                    prevPos = currentPos;
                    hitPoint = currentPos;
                }
            }

            if (m_LandingMarker != null)
            {
                m_LandingMarker.transform.position = hitPoint + Vector3.up * 0.05f;
                m_LandingMarker.SetActive(hitGround);
            }
        }

        #endregion

        #region Helper Methods

        private void FindVisualModel()
        {
            if (m_RoleVisuals != null && m_RoleVisuals.dadModel != null)
            {
                m_VisualModel = m_RoleVisuals.dadModel.transform;
            }
            else
            {
                var anim = GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    m_VisualModel = anim.transform;
                }
                else if (transform.childCount > 0)
                {
                    m_VisualModel = transform.GetChild(0);
                }
            }

            if (m_VisualModel != null)
            {
                m_OriginalModelScale = m_VisualModel.localScale;
                m_OriginalModelPos = m_VisualModel.localPosition;
                m_OriginalModelRot = m_VisualModel.localRotation;
            }
        }

        private void FindPartnerIfNeeded()
        {
            if (m_PartnerTransform != null) return;

            // 1. Quét tìm Dummy Companion trước (chế độ Tutorial)
            var dummy = FindAnyObjectByType<SoloCompanionDummy>();
            if (dummy != null && dummy.gameObject != this.gameObject)
            {
                m_PartnerTransform = dummy.transform;
                m_PartnerBoostAbility = dummy.GetComponent<CoopBoostThrowAbility>();
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
                        m_PartnerBoostAbility = client.PlayerObject.GetComponent<CoopBoostThrowAbility>();
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
                    m_PartnerBoostAbility = player.GetComponent<CoopBoostThrowAbility>();
                    return;
                }
            }

            // 4. Fallback: Quét các component CoopBoostThrowAbility trong Scene
            var allBoostAbilities = FindObjectsByType<CoopBoostThrowAbility>();
            foreach (var ability in allBoostAbilities)
            {
                if (ability != null && ability.gameObject != this.gameObject)
                {
                    m_PartnerTransform = ability.transform;
                    m_PartnerBoostAbility = ability;
                    return;
                }
            }
        }

        private void CreateGUIStyles()
        {
            m_BarBgTexture = new Texture2D(1, 1);
            m_BarBgTexture.SetPixel(0, 0, new Color(0.08f, 0.09f, 0.12f, 0.88f));
            m_BarBgTexture.Apply();

            m_BarFillTexture = new Texture2D(1, 1);
            m_BarFillTexture.SetPixel(0, 0, Color.white);
            m_BarFillTexture.Apply();
        }

        #endregion

        #region Force Bar GUI (Thanh Nộ Canh Lực - Minimalist, No Text)

        private void OnGUI()
        {
            // CHỈ NGƯỜI CHƠI THẬT MỚI VẼ GIAO DIỆN (DUMMY BOT KHÔNG VẼ)
            if (!IsLocalPlayerCharacter) return;

            // KHI ĐANG GỒNG TÍCH LỰC (CHARGING RAGE / FORCE GAUGE - CHỈ BA GỒNG)
            // Thiết kế thanh nộ tối giản, sang trọng chuẩn game AAA, KHÔNG CHỮ để tránh rối mắt UI
            if (m_IsCharging && IsDad)
            {
                float barWidth = 260f;
                float barHeight = 8f;
                float posX = (Screen.width - barWidth) / 2f;
                float posY = Screen.height - 110f;

                // Nền đen bán trong suốt bao ngoài thanh nộ
                GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.85f);
                GUI.DrawTexture(new Rect(posX - 4f, posY - 4f, barWidth + 8f, barHeight + 8f), m_BarBgTexture);

                // Nền rãnh thanh nộ
                GUI.color = new Color(0.18f, 0.20f, 0.26f, 0.90f);
                GUI.DrawTexture(new Rect(posX, posY, barWidth, barHeight), m_BarFillTexture);

                // Màu sắc thanh nộ: chuyển mượt từ Cam Vàng -> Đỏ Rực (Fiery Rage) theo độ gồng
                Color rageColor;
                if (m_CurrentChargeRatio < 0.70f)
                {
                    rageColor = Color.Lerp(new Color(1.0f, 0.78f, 0.15f), new Color(1.0f, 0.48f, 0.10f), m_CurrentChargeRatio / 0.70f);
                }
                else
                {
                    float maxPulse = (m_CurrentChargeRatio >= 0.95f) ? Mathf.PingPong(Time.time * 9f, 0.25f) : 0f;
                    rageColor = new Color(1.0f, 0.18f + maxPulse, 0.12f, 1f);
                }

                // Vẽ phần nạp lực hiện tại
                GUI.color = rageColor;
                float currentFillWidth = Mathf.Max(4f, barWidth * m_CurrentChargeRatio);
                GUI.DrawTexture(new Rect(posX, posY, currentFillWidth, barHeight), m_BarFillTexture);

                // Vạch chia tỉ lệ 25%, 50%, 75% siêu mảnh để dễ canh nấc lực
                GUI.color = new Color(0.08f, 0.08f, 0.10f, 0.70f);
                GUI.DrawTexture(new Rect(posX + barWidth * 0.25f - 0.5f, posY, 1.5f, barHeight), m_BarFillTexture);
                GUI.DrawTexture(new Rect(posX + barWidth * 0.50f - 0.5f, posY, 1.5f, barHeight), m_BarFillTexture);
                GUI.DrawTexture(new Rect(posX + barWidth * 0.75f - 0.5f, posY, 1.5f, barHeight), m_BarFillTexture);

                GUI.color = Color.white;
            }
        }

        #endregion
    }
}
