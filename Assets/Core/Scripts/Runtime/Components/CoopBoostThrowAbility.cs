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

        #endregion

        #region Private Fields

        private CoreMovement m_Motor;
        private PlayerRoleVisuals m_RoleVisuals;
        private CoreStatsHandler m_StatsHandler;
        private Animator m_Animator;
        private CharacterController m_CharacterController;

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

        public bool IsDad => m_RoleVisuals != null ? m_RoleVisuals.IsDad : gameObject.name.ToLower().Contains("dad");
        public bool IsCharging => m_IsCharging;
        public float CurrentChargeRatio => m_CurrentChargeRatio;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            m_Motor = GetComponent<CoreMovement>();
            m_RoleVisuals = GetComponent<PlayerRoleVisuals>();
            m_StatsHandler = GetComponent<CoreStatsHandler>();
            m_Animator = GetComponentInChildren<Animator>();
            m_CharacterController = GetComponent<CharacterController>();

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

            // Xử lý logic gồng và ném
            if (IsDad)
            {
                HandleDadCharging(fPressedThisFrame, fHeld, fReleasedThisFrame, dist);
            }
            else
            {
                // Nếu người chơi thật điều khiển Con: Bấm F để đạp vai Ba bật nhảy lên
                if (fPressedThisFrame && m_CanBoostPartner)
                {
                    ExecuteChildLaunch();
                }
            }
        }

        private void OnDisable()
        {
            if (m_IsCharging) CancelCharge();
            if (m_TrajectoryLine != null) m_TrajectoryLine.enabled = false;
            if (m_LandingMarker != null) m_LandingMarker.SetActive(false);
        }

        #endregion

        #region Dad Charging & Throw Handling

        private void HandleDadCharging(bool fPressedThisFrame, bool fHeld, bool fReleasedThisFrame, float partnerDistance)
        {
            // 1. BẮT ĐẦU GỒNG (F Pressed)
            if (fPressedThisFrame && m_CanBoostPartner && !m_IsCharging)
            {
                StartCharging();
            }

            // 2. ĐANG GỒNG TÍCH LỰC (F Held)
            if (m_IsCharging)
            {
                // Hủy nếu partner đi quá xa hoặc phím Esc
                if (partnerDistance > maxBoostDistance * 1.8f)
                {
                    CancelCharge();
                    return;
                }

                m_CurrentChargeTime += Time.deltaTime;
                m_CurrentChargeRatio = Mathf.Clamp01(m_CurrentChargeTime / maxChargeDuration);

                // Giữ Con đứng sát phía trước chuẩn bị được nâng
                if (m_PartnerTransform != null)
                {
                    Vector3 prepHoldPos = transform.position + transform.forward * 0.75f + Vector3.up * 0.4f;
                    m_PartnerTransform.position = Vector3.Lerp(m_PartnerTransform.position, prepHoldPos, Time.deltaTime * 6f);
                }

                // Procedural Animation: Gồng cơ bắp (Squash Y & Tremble Shake)
                ApplySquashAndTremble(m_CurrentChargeRatio);

                // Cập nhật đường đạn dự đoán Parabolic Arc
                UpdateTrajectoryArc(m_CurrentChargeRatio);

                // 3. THẢ F -> QUĂNG CON ĐI!
                if (fReleasedThisFrame)
                {
                    if (m_CurrentChargeTime >= minHoldThreshold)
                    {
                        ExecuteChargedThrow(m_CurrentChargeRatio);
                    }
                    else
                    {
                        CancelCharge();
                    }
                }
            }
        }

        private void StartCharging()
        {
            m_IsCharging = true;
            m_CurrentChargeTime = 0f;
            m_CurrentChargeRatio = 0f;

            if (m_TrajectoryLine != null) m_TrajectoryLine.enabled = true;
            if (m_LandingMarker != null) m_LandingMarker.SetActive(true);

            UISoundManager.PlayClick();
            Debug.Log("[CoopBoost] 🧔 Ba bắt đầu gồng cơ bắp tích lực ném...");
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

        private void ExecuteChargedThrow(float ratio)
        {
            // Tính toán lực ném theo tỷ lệ gồng
            float finalVerticalVel = Mathf.Lerp(minVerticalBoost, maxVerticalBoost, ratio);
            float finalForwardForce = Mathf.Lerp(minForwardBoostForce, maxForwardBoostForce, ratio);
            float staminaCost = Mathf.Lerp(dadStaminaCostMin, dadStaminaCostMax, ratio);

            // Kiểm tra và tiêu hao thể lực của Ba
            if (m_StatsHandler != null)
            {
                m_StatsHandler.TryConsumeStat(Animator.StringToHash("Stamina"), staminaCost);
            }

            Vector3 launchDirection = transform.forward;

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
            Debug.Log($"[CoopBoost] 🔥 🧔 Ba QUĂNG CON ĐI! Lực: {(int)(ratio * 100)}% (V_Vel={finalVerticalVel:F1}m/s, Fwd={finalForwardForce:F1})");
        }

        private void CancelCharge()
        {
            m_IsCharging = false;
            m_CurrentChargeTime = 0f;
            m_CurrentChargeRatio = 0f;

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

        private void ExecuteChildLaunch()
        {
            m_CooldownTimer = boostCooldown;
            Vector3 launchDirection = m_PartnerTransform != null ? m_PartnerTransform.forward : transform.forward;
            ApplyLaunchLocally(launchDirection, minVerticalBoost + 3.0f, minForwardBoostForce + 3.0f);
            UISoundManager.PlayClick();
            Debug.Log("[CoopBoost] 👶 Con đã đạp lên vai Ba bật nhảy lên cao!");
        }

        [Rpc(SendTo.Owner)]
        public void ReceiveBoostLaunchRpc(Vector3 forwardDirection, float verticalVelocity, float forwardForce)
        {
            ApplyLaunchLocally(forwardDirection, verticalVelocity, forwardForce);
        }

        /// <summary>
        /// Kích hoạt cơ chế bay xa theo quán tính phương ngang và bổng lên theo phương đứng.
        /// </summary>
        public void ApplyLaunchLocally(Vector3 forwardDirection, float verticalVelocity, float forwardForce)
        {
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

        private void UpdateTrajectoryArc(float ratio)
        {
            if (m_TrajectoryLine == null) return;

            float vY = Mathf.Lerp(minVerticalBoost, maxVerticalBoost, ratio);
            float fF = Mathf.Lerp(minForwardBoostForce, maxForwardBoostForce, ratio);

            Vector3 startPos = transform.position + transform.forward * 0.6f + Vector3.up * 1.3f;
            Vector3 velocity = transform.forward * fF + Vector3.up * vY;
            Vector3 gravity = Physics.gravity;

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

            // 1. Quét tìm Dummy Companion trước
            var dummy = FindAnyObjectByType<SoloCompanionDummy>();
            if (dummy != null && dummy.gameObject != this.gameObject)
            {
                m_PartnerTransform = dummy.transform;
                m_PartnerBoostAbility = dummy.GetComponent<CoopBoostThrowAbility>();
                return;
            }

            // 2. Quét qua AllPlayers
            foreach (var player in PlayerRoleVisuals.AllPlayers)
            {
                if (player != null && player.gameObject != this.gameObject)
                {
                    m_PartnerTransform = player.transform;
                    m_PartnerBoostAbility = player.GetComponent<CoopBoostThrowAbility>();
                    return;
                }
            }

            // 3. Fallback: Quét các component CoopBoostThrowAbility trong Scene
            var allBoostAbilities = FindObjectsByType<CoopBoostThrowAbility>(FindObjectsSortMode.None);
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

        #region Force Bar GUI (Thanh Canh Lực)

        private void OnGUI()
        {
            // CHỈ NGƯỜI CHƠI THẬT MỚI VẼ GIAO DIỆN (DUMMY BOT KHÔNG VẼ)
            if (!IsLocalPlayerCharacter) return;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = Color.white;

            var badgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            // 1. KHI ĐANG GỒNG TÍCH LỰC (CHARGING FORCE GAUGE)
            if (m_IsCharging)
            {
                float barWidth = 420f;
                float barHeight = 28f;
                float panelWidth = 460f;
                float panelHeight = 90f;
                float posX = (Screen.width - panelWidth) / 2f;
                float posY = Screen.height - 210f;

                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(posX, posY, panelWidth, panelHeight), m_BarBgTexture);
                GUI.Box(new Rect(posX, posY, panelWidth, panelHeight), GUIContent.none);

                int percent = Mathf.RoundToInt(m_CurrentChargeRatio * 100f);
                string title = $"⚡ GỒNG LỰC NÉM: {percent}% (THẢ [F] ĐỂ QUĂNG!)";
                GUI.Label(new Rect(posX, posY + 6f, panelWidth, 24f), title, titleStyle);

                float innerX = posX + 20f;
                float innerY = posY + 34f;
                GUI.color = new Color(0.2f, 0.25f, 0.35f, 1f);
                GUI.DrawTexture(new Rect(innerX, innerY, barWidth, barHeight), m_BarFillTexture);

                Color fillColor;
                string statusText;
                if (m_CurrentChargeRatio < 0.35f)
                {
                    fillColor = new Color(0.18f, 0.85f, 0.45f, 1f);
                    statusText = "🌱 [LỰC NHẸ - ĐẨY LÊN GỜ THẤP]";
                    badgeStyle.normal.textColor = fillColor;
                }
                else if (m_CurrentChargeRatio < 0.70f)
                {
                    fillColor = new Color(0.98f, 0.82f, 0.15f, 1f);
                    statusText = "⭐ [LỰC VỪA - VƯỢT HỐ NỨT / BỜ TƯỜNG]";
                    badgeStyle.normal.textColor = fillColor;
                }
                else if (m_CurrentChargeRatio < 0.90f)
                {
                    fillColor = new Color(1.0f, 0.55f, 0.10f, 1f);
                    statusText = "🔥 [LỰC MẠNH - PHÓNG VÁCH NÚI CAO]";
                    badgeStyle.normal.textColor = fillColor;
                }
                else
                {
                    float pulse = Mathf.PingPong(Time.time * 8f, 0.2f);
                    fillColor = new Color(1.0f, 0.15f + pulse, 0.20f, 1f);
                    statusText = "💥 [CỰC ĐẠI 100% - SIÊU PHÓNG TỐI ĐA!]";
                    badgeStyle.normal.textColor = Color.yellow;
                }

                GUI.color = fillColor;
                float currentFillWidth = Mathf.Max(6f, barWidth * m_CurrentChargeRatio);
                GUI.DrawTexture(new Rect(innerX, innerY, currentFillWidth, barHeight), m_BarFillTexture);

                GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);
                GUI.DrawTexture(new Rect(innerX + barWidth * 0.25f - 1f, innerY, 2f, barHeight), m_BarFillTexture);
                GUI.DrawTexture(new Rect(innerX + barWidth * 0.50f - 1f, innerY, 2f, barHeight), m_BarFillTexture);
                GUI.DrawTexture(new Rect(innerX + barWidth * 0.75f - 1f, innerY, 2f, barHeight), m_BarFillTexture);

                GUI.color = Color.white;
                GUI.Label(new Rect(posX, innerY + barHeight + 4f, panelWidth, 20f), statusText, badgeStyle);
            }
            // 2. KHI ĐỨNG GẦN VÀ CHƯA GỒNG (IDLE PROMPT)
            else if (m_CanBoostPartner)
            {
                var boxStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                boxStyle.normal.textColor = Color.white;

                string prompt = IsDad
                    ? "🚀 GIỮ CHẶT [F] ĐỂ GỒNG LỰC NÉM CON (Thả [F] Để Quăng!)"
                    : "🚀 BẤM [F] ĐỂ ĐẠP VAI BA BẬT PHÓNG LÊN CAO";

                GUI.color = new Color(0.15f, 0.95f, 0.45f, 0.95f);
                GUI.Box(new Rect(Screen.width / 2f - 260, Screen.height - 165, 520, 44), prompt, boxStyle);
            }
        }

        #endregion
    }
}
