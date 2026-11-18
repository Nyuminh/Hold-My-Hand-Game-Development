using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Cơ chế Lái Thuyền Co-op (Co-op Wooden Boat Gameplay):
    /// - 🧔 Người cha (Dad): Ngồi tại ghế chèo (Player1_Rower_MountPoint), chèo 2 mái chèo (Oar_Left, Oar_Right).
    ///   Khi người cha chèo, thuyền chỉ đi thẳng về phía trước theo hướng mũi thuyền.
    /// - 👶 Người con (Child): Ngồi tại vị trí bẻ lái (Player2_Navigator_MountPoint), điều khiển cần lái (Rudder_Assembly).
    ///   Cơ chế lái đòn bẩy thực tế:
    ///   + Cần lái bẻ sang PHẢI (phím D) -> Thuyền lái sang TRÁI.
    ///   + Cần lái bẻ sang TRÁI (phím A) -> Thuyền lái sang PHẢI.
    /// - Nhấn [E] để lên / xuống thuyền.
    /// </summary>
    public class CoopWoodenBoat : NetworkBehaviour
    {
        #region Serialized Fields

        [Header("Gắn kết các bộ phận thuyền (Boat Transforms)")]
        [Tooltip("Vị trí ngồi của Ba (Rower Mount Point)")]
        [SerializeField] private Transform rowerMountPoint;

        [Tooltip("Vị trí ngồi của Con (Navigator / Rudder Mount Point)")]
        [SerializeField] private Transform navigatorMountPoint;

        [Tooltip("Cụm cần lái & bánh lái (Rudder Assembly)")]
        [SerializeField] private Transform rudderAssembly;

        [Tooltip("Mái chèo bên trái (Oar Left)")]
        [SerializeField] private Transform oarLeft;

        [Tooltip("Mái chèo bên phải (Oar Right)")]
        [SerializeField] private Transform oarRight;

        [Tooltip("Ghế ngồi chèo thuyền")]
        [SerializeField] private Transform seatRower;

        [Tooltip("Ghế ngồi bẻ lái")]
        [SerializeField] private Transform seatNavigator;

#pragma warning disable 0414
        [Tooltip("Quãng đường thuyền tiến được cho mỗi nhịp nhấn Space (mét)")]
        [SerializeField] private float distancePerStroke = 1.0f;
#pragma warning restore 0414

        [Tooltip("Vận tốc khởi đầu khi nhấn 1 nhịp Space (mét/giây)")]
        [SerializeField] private float minStrokeSpeed = 2.2f;

        [Tooltip("Gia tăng vận tốc tức thời mỗi khi spam Space nhanh (mét/giây)")]
        [SerializeField] private float speedBoostPerSpam = 1.2f;

        [Tooltip("Tốc độ tiến tối đa khi Ba spam chèo thuyền (mét/giây)")]
        [SerializeField] private float maxRowSpeed = 7.5f;

        [Tooltip("Thời lượng của 1 nhịp chèo mái chèo (giây)")]
        [SerializeField] private float strokeDuration = 0.55f;

#pragma warning disable 0414
        [Tooltip("Gia tốc khi chèo thuyền (mét/giây^2)")]
        [SerializeField] private float rowAcceleration = 2.4f;
#pragma warning restore 0414

        [Tooltip("Lực cản nước làm giảm tốc độ khi thuyền trôi theo quán tính (mét/giây^2). Giá trị càng nhỏ thuyền trôi quán tính càng xa và lâu.")]
        [SerializeField] private float waterDrag = 1.6f;

#pragma warning disable 0414
        [Tooltip("Tốc độ lùi tối đa khi chèo lùi (mét/giây)")]
        [SerializeField] private float reverseSpeed = 1.5f;

        [Tooltip("Tốc độ nhịp chèo (chu kỳ/giây)")]
        [SerializeField] private float strokeFrequency = 3.0f;
#pragma warning restore 0414

        [Tooltip("Góc quét của mái chèo khi chèo (độ)")]
        [SerializeField] private float oarStrokeAngle = 24.0f;

        [Tooltip("Góc cắm mái chèo xuống nước khi kéo (độ)")]
        [SerializeField] private float oarDipAngle = 8.0f;

        [Header("Thông số Cần bẻ lái (Child Rudder Steering)")]
        [Tooltip("Góc quay tối đa của cần lái (độ)")]
        [SerializeField] private float maxTillerAngle = 30.0f;

        [Tooltip("Tốc độ bẻ cần lái qua lại (độ/giây)")]
        [SerializeField] private float tillerTurnSpeed = 80.0f;

        [Tooltip("Độ nhạy quay thuyền (độ/giây)")]
        [SerializeField] private float boatTurnSensitivity = 42.0f;

        [Tooltip("Quy tắc bẻ cần: Cần sang Phải -> Thuyền rẽ Trái và ngược lại (mặc định true)")]
        [SerializeField] private bool invertTillerSteering = true;

        [Header("Mặt nước & Độ nổi (Water & Buoyancy)")]
        [Tooltip("Độ cao mặt nước trong scene")]
        [SerializeField] private float waterSurfaceY = 0.50f;

        [Tooltip("Độ cao thân thuyền so với mặt nước")]
        [SerializeField] private float restingHeightOffset = 0.03f;

        [Tooltip("Tần số sóng nhấp nhô")]
        [SerializeField] private float bobbingFrequency = 1.8f;

        [Tooltip("Biên độ sóng nhấp nhô (mét)")]
        [SerializeField] private float bobbingAmplitude = 0.025f;

#pragma warning disable 0414
        [Tooltip("Biên độ lắc lư nhẹ theo sóng (độ)")]
        [SerializeField] private float gentleRollAmplitude = 1.0f;
#pragma warning restore 0414

        [Header("Tương tác & Lên thuyền (Interaction)")]
        [Tooltip("Bán kính có thể nhấn [E] lên thuyền (mét)")]
        [SerializeField] private float boardInteractionRadius = 5.5f;

#pragma warning disable 0414
        [Tooltip("Phím lên/xuống thuyền (Legacy Input)")]
        [SerializeField] private KeyCode boardKey = KeyCode.E;
#pragma warning restore 0414

        [Tooltip("Bật chế độ thử nghiệm Solo: khi chỉ có 1 người chơi thì người đó được chèo lẫn lái")]
        [SerializeField] private bool soloTestMode = true;

        [Header("River expedition")]
        [SerializeField] private bool riverPhysicsEnabled;
        [SerializeField] private float maxDriftSpeed = 5f;
        private Vector3 m_ExternalDrift;
        private float m_LastStrokeAt = -10f;
        private float m_LastImpactAt = -10f;
        private float m_DashUntil;
        private float m_NextDash;
        public void BeginRiverDash()
        {
            if (!HasSimulationAuthority || !HasCoopCrew || !HasRecentStroke || Time.time < m_NextDash) return;
            m_DashUntil = Time.time + 1.2f; m_NextDash = Time.time + 2f;
        }
        public bool HasSimulationAuthority => !IsSpawned || IsServer;
        public Vector3 ExternalDriftVelocity => m_ExternalDrift;
        public bool HasRecentStroke => Time.time - m_LastStrokeAt < 0.55f;
        public bool HasCoopCrew => IsDadMounted && IsChildMounted;
        public bool IsCounterSteering(float rotationSign) => Mathf.Abs(m_ChildSteerInput) > 0.35f && Mathf.Sign(m_ChildSteerInput) == Mathf.Sign(rotationSign);
        public void LimitWhirlpoolPropulsion(bool escaping)
        {
            if (HasSimulationAuthority && !escaping) m_CurrentSpeed = Mathf.Min(m_CurrentSpeed, 1.2f);
        }
        public void WarnRiverHazard()
        {
            if (!HasSimulationAuthority) return;
            if (IsSpawned) RiverWarningRpc(); else GetComponent<LevelTwoFeedback>()?.Play(0.15f);
        }
        [Rpc(SendTo.Everyone)]
        private void RiverWarningRpc() => GetComponent<LevelTwoFeedback>()?.Play(0.15f);

        // Acceleration is integrated once per physics step by each water volume.
        public void AddExternalDriftForce(Vector3 acceleration, float deltaTime)
        {
            if (!HasSimulationAuthority || !riverPhysicsEnabled || !HasCoopCrew) return;
            acceleration.y = 0f;
            m_ExternalDrift = Vector3.ClampMagnitude(m_ExternalDrift + acceleration * Mathf.Clamp(deltaTime, 0f, 0.1f), maxDriftSpeed);
        }

        public void ApplyRockImpact(Vector3 normal)
        {
            if (!HasSimulationAuthority || Time.time - m_LastImpactAt < 0.35f) return;
            m_LastImpactAt = Time.time;
            if (riverPhysicsEnabled)
                FindAnyObjectByType<CampaignCheckpointController>()?.BoatImpact(Mathf.Abs(m_CurrentSpeed) + m_ExternalDrift.magnitude);
            normal.y = 0f;
            m_CurrentSpeed *= 0.25f;
            m_ExternalDrift = normal.normalized * 1.8f;
            if (IsSpawned) RiverImpactRpc(); else GetComponent<LevelTwoFeedback>()?.Play(0.4f);
        }

        [Rpc(SendTo.Everyone)]
        private void RiverImpactRpc() => GetComponent<LevelTwoFeedback>()?.Play(0.4f);

        [Header("Độ lệch vị trí ngồi (Seat Offsets)")]
        [SerializeField] private Vector3 dadSeatLocalOffset = new Vector3(0f, 0.06f, 0f);
        [SerializeField] private Vector3 childSeatLocalOffset = new Vector3(0f, 0.06f, 0f);

        #endregion

        #region Private Fields

        // Trạng thái nhân vật đang ngồi trên thuyền
        private GameObject m_DadPlayer = null;
        private GameObject m_ChildPlayer = null;

        private CharacterController m_DadCC = null;
        private CharacterController m_ChildCC = null;

        private CoreMovement m_DadMovement = null;
        private CoreMovement m_ChildMovement = null;

        private Animator m_DadAnimator = null;
        private Animator m_ChildAnimator = null;

        // Trạng thái vật lý thuyền
        private float m_CurrentSpeed = 0f;
        private float m_CurrentTillerAngle = 0f;
        private bool m_IsDadRowing = false;
        private float m_ChildSteerInput = 0f;

        // Tiến trình nhịp chèo theo phím Space
        private float m_StrokePhase = 1f;              // [0 -> 1]: Tiến trình 1 nhịp chèo (1f = nghỉ)
        private bool m_IsStrokeActive = false;         // true khi đang diễn ra nhịp chèo
        private bool m_IsGroundedOnLand = false;       // true khi thuyền bị mắc cạn trên bờ đất (không ở dưới nước)

        // Góc ban đầu của các bộ phận
        private Quaternion m_OarLeftInitialRot = Quaternion.identity;
        private Quaternion m_OarRightInitialRot = Quaternion.identity;
        private Quaternion m_RudderInitialRot = Quaternion.identity;

        // Va chạm chướng ngại vật
        private Collider m_BoatCollider = null;

        // Network synchronization variables (Server authoritative)
        private readonly NetworkVariable<Vector3> m_NetBoatPosition = new NetworkVariable<Vector3>(
            Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<Quaternion> m_NetBoatRotation = new NetworkVariable<Quaternion>(
            Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> m_NetSpeed = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> m_NetTiller = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> m_NetIsRowing = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> m_NetRowCycleTime = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> m_NetIsGrounded = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<NetworkObjectReference> m_NetDadPlayer = new NetworkVariable<NetworkObjectReference>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<NetworkObjectReference> m_NetChildPlayer = new NetworkVariable<NetworkObjectReference>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private static readonly int s_PiggybackingHash = Animator.StringToHash("IsPiggybacking");
        private static readonly int s_SittingHash = Animator.StringToHash("IsSitting");

        #endregion

        #region Properties

        public bool IsDadMounted => m_DadPlayer != null;
        public bool IsChildMounted => m_ChildPlayer != null;
        public float CurrentSpeed => m_CurrentSpeed;
        public float CurrentTillerAngle => m_CurrentTillerAngle;
        public bool IsRowing => m_IsDadRowing;
        public bool IsGroundedOnLand => m_IsGroundedOnLand;

        /// <summary>
        /// Guard an toan: chi cho phep ghi NetworkVariable khi NetworkManager
        /// van dang hoat dong. Tranh log loi cosmetic khi thoat Play Mode.
        /// </summary>
        private bool CanWriteNetVars => IsSpawned && NetworkManager != null && NetworkManager.IsListening && m_NetBoatPosition.CanClientWrite(NetworkManager.LocalClientId);

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (boardInteractionRadius < 5.0f) boardInteractionRadius = 5.5f;
            AutoAssignReferences();
            StoreInitialTransforms();
            EnsureCollider();
        }

        private void Start()
        {
            StoreInitialTransforms();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (CanWriteNetVars)
            {
                m_NetBoatPosition.Value = transform.position;
                m_NetBoatRotation.Value = transform.rotation;
                m_NetSpeed.Value = 0f;
                m_NetTiller.Value = 0f;
                m_NetIsRowing.Value = false;
                m_NetRowCycleTime.Value = 0f;
                m_NetIsGrounded.Value = false;
                if (m_NetDadPlayer.CanClientWrite(NetworkManager.LocalClientId)) m_NetDadPlayer.Value = default;
                if (m_NetChildPlayer.CanClientWrite(NetworkManager.LocalClientId)) m_NetChildPlayer.Value = default;
            }
            else
            {
                m_NetDadPlayer.OnValueChanged += OnNetDadPlayerChanged;
                m_NetChildPlayer.OnValueChanged += OnNetChildPlayerChanged;

                if (m_NetBoatPosition.Value != Vector3.zero)
                {
                    transform.position = m_NetBoatPosition.Value;
                    transform.rotation = m_NetBoatRotation.Value;
                }

                OnNetDadPlayerChanged(default, m_NetDadPlayer.Value);
                OnNetChildPlayerChanged(default, m_NetChildPlayer.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
            {
                m_NetDadPlayer.OnValueChanged -= OnNetDadPlayerChanged;
                m_NetChildPlayer.OnValueChanged -= OnNetChildPlayerChanged;
            }
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            HandlePlayerMountInputs();
            ReadMountedPlayerControls();

            if (!IsSpawned || IsServer)
            {
                // Server hoặc Local Singleplayer: Mô phỏng vật lý và sóng nước
                SimulateBoatPhysics();
                UpdateBuoyancyBobbing();

                // Cập nhật tiến trình nhịp chèo theo phím Space
                if (m_IsStrokeActive)
                {
                    float effectiveDuration = Mathf.Clamp(strokeDuration / (1f + m_CurrentSpeed * 0.12f), 0.22f, strokeDuration);
                    m_StrokePhase += Time.deltaTime / effectiveDuration;
                    if (m_StrokePhase >= 1f)
                    {
                        m_StrokePhase = 1f;
                        m_IsStrokeActive = false;
                        m_IsDadRowing = false;
                    }
                }

                // Ghi vào NetworkVariable đồng bộ xuống các Client
                if (CanWriteNetVars)
                {
                    m_NetBoatPosition.Value = transform.position;
                    m_NetBoatRotation.Value = transform.rotation;
                    m_NetSpeed.Value = m_CurrentSpeed;
                    m_NetTiller.Value = m_CurrentTillerAngle;
                    m_NetIsRowing.Value = m_IsDadRowing;
                    m_NetRowCycleTime.Value = m_StrokePhase;
                    m_NetIsGrounded.Value = m_IsGroundedOnLand;
                }
            }
            else
            {
                // Client: Đồng bộ mượt mà từ Server
                SyncTransformFromNetwork();

                // Cập nhật tiến trình nhịp chèo trên Client theo nhịp Space của Ba
                if (m_IsStrokeActive)
                {
                    float effectiveDuration = Mathf.Clamp(strokeDuration / (1f + m_CurrentSpeed * 0.12f), 0.22f, strokeDuration);
                    m_StrokePhase += Time.deltaTime / effectiveDuration;
                    if (m_StrokePhase >= 1f)
                    {
                        m_StrokePhase = 1f;
                        m_IsStrokeActive = false;
                        m_IsDadRowing = false;
                    }
                }
            }

            AnimateOarsAndRudder();
        }

        private void LateUpdate()
        {
            if (IsSpawned && !IsServer)
            {
                if (m_DadPlayer == null && m_NetDadPlayer.Value.TryGet(out NetworkObject dadNet))
                {
                    MountPlayer(dadNet.gameObject, true);
                }
                if (m_ChildPlayer == null && m_NetChildPlayer.Value.TryGet(out NetworkObject childNet))
                {
                    MountPlayer(childNet.gameObject, false);
                }
            }

            StickPlayersToSeats();
        }

        #endregion

        #region Setup & Initialization

        private void AutoAssignReferences()
        {
            if (rowerMountPoint == null) rowerMountPoint = transform.Find("Player1_Rower_MountPoint");
            if (navigatorMountPoint == null) navigatorMountPoint = transform.Find("Player2_Navigator_MountPoint");
            if (rudderAssembly == null) rudderAssembly = transform.Find("Rudder_Assembly");
            if (oarLeft == null) oarLeft = transform.Find("Oar_Left");
            if (oarRight == null) oarRight = transform.Find("Oar_Right");
            if (seatRower == null) seatRower = transform.Find("Seat_Rower");
            if (seatNavigator == null) seatNavigator = transform.Find("Seat_Navigator");

            if (rowerMountPoint == null && seatRower != null) rowerMountPoint = seatRower;
            if (navigatorMountPoint == null && seatNavigator != null) navigatorMountPoint = seatNavigator;
        }

        private void StoreInitialTransforms()
        {
            if (oarLeft != null) m_OarLeftInitialRot = oarLeft.localRotation;
            if (oarRight != null) m_OarRightInitialRot = oarRight.localRotation;
            if (rudderAssembly != null) m_RudderInitialRot = rudderAssembly.localRotation;
        }

        private void EnsureCollider()
        {
            m_BoatCollider = GetComponent<Collider>();
            if (m_BoatCollider == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                // Thân thuyền dài ~4.5m, rộng ~1.6m, cao ~0.8m theo trục Blender Z-up
                box.center = new Vector3(0f, -0.6f, 0.35f);
                box.size = new Vector3(1.7f, 4.2f, 0.9f);
                m_BoatCollider = box;
            }
        }

        #endregion

        #region Boarding & Mounting Logic

        /// <summary>
        /// Xử lý nhấn [E] để người chơi bước lên hoặc rời thuyền.
        /// Thêm log chi tiết cho từng bước chẩn đoán lỗi.
        /// </summary>
        private void HandlePlayerMountInputs()
        {
            if (TutorialIntroCutscene.IsAnyPlaying) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Phím tắt kiểm thử nhanh: [B] Dịch chuyển người chơi và bạn đồng hành đến cạnh thuyền
            bool bPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame) bPressed = true;
#else
            if (Input.GetKeyDown(KeyCode.B)) bPressed = true;
#endif

            if (bPressed)
            {
                var p = FindLocalPlayer();
                if (p != null)
                {
                    var cc = p.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    p.transform.position = transform.position + GetBoatWorldRight() * 2.2f + Vector3.up * 0.4f;
                    if (cc != null) cc.enabled = true;

                    var dummy = FindAnyObjectByType<SoloCompanionDummy>();
                    if (dummy != null)
                    {
                        var dummyCC = dummy.GetComponent<CharacterController>();
                        if (dummyCC != null) dummyCC.enabled = false;
                        dummy.transform.position = transform.position + GetBoatWorldRight() * 2.2f + Vector3.forward * 1.5f + Vector3.up * 0.4f;
                        if (dummyCC != null) dummyCC.enabled = true;
                    }

                    Debug.Log("[CoopWoodenBoat] 🚀 Phím [B]: Đã dịch chuyển người chơi và bạn đồng hành đến cạnh thuyền!");
                }
                else
                {
                    Debug.LogWarning("[CoopWoodenBoat] ⚠️ Phím [B]: Không tìm thấy nhân vật người chơi để dịch chuyển.");
                }
            }
#endif

            bool ePressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                ePressed = true;
            }
#else
            if (Input.GetKeyDown(boardKey))
            {
                ePressed = true;
            }
#endif

            if (!ePressed) return;

            Debug.Log("[CoopWoodenBoat] 🎮 Bấm phím [E] tương tác thuyền!");

            // 1. Tìm nhân vật của người chơi cục bộ
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null)
            {
                Debug.LogError("[CoopWoodenBoat] ❌ LỖI: Không tìm thấy GameObject của LocalPlayer trong Scene! Hãy kiểm tra spawn nhân vật.");
                return;
            }

            bool isMeDad = m_DadPlayer == localPlayer;
            bool isMeChild = m_ChildPlayer == localPlayer;

            // 2. Kiểm tra xem người chơi này đang ngồi trên thuyền chưa -> Xuống thuyền
            if (isMeDad || isMeChild)
            {
                Debug.Log($"[CoopWoodenBoat] 🚪 Người chơi {localPlayer.name} đang ngồi trên thuyền -> Thực hiện XUỐNG THUYỀN (Dismount)!");
                if (IsSpawned && !IsServer)
                {
                    RequestDismountServerRpc(isMeDad);
                }
                else
                {
                    DismountPlayer(isMeDad);
                    if (CanWriteNetVars)
                    {
                        if (isMeDad && m_NetDadPlayer.CanClientWrite(NetworkManager.LocalClientId)) m_NetDadPlayer.Value = default;
                        else if (m_NetChildPlayer.CanClientWrite(NetworkManager.LocalClientId)) m_NetChildPlayer.Value = default;
                    }
                }
                return;
            }

            // 3. Nếu chưa lên thuyền, đo khoảng cách tới thuyền
            float dist = GetDistanceToPlayer(localPlayer.transform.position);
            bool isDad = IsPlayerDad(localPlayer);

            Debug.Log($"[CoopWoodenBoat] 📍 Khoảng cách tới thuyền: {dist:F2}m (Bán kính cho phép: {boardInteractionRadius:F1}m) | Người chơi: '{localPlayer.name}' | Vai trò nhận diện: {(isDad ? "🧔 Ba (Chèo thuyền)" : "👶 Con (Bẻ lái)")}");

            if (dist > boardInteractionRadius)
            {
                Debug.LogWarning($"[CoopWoodenBoat] ⚠️ Quá xa thuyền ({dist:F2}m > {boardInteractionRadius:F1}m)! Vui lòng bước lại gần mạn thuyền hoặc mép cầu tàu để lên thuyền.");
                return;
            }
                //123
            // 4. Kiểm tra chỗ ngồi theo vai trò
            if (isDad)
            {
                if (m_DadPlayer != null)
                {
                    Debug.LogWarning($"[CoopWoodenBoat] ⚠️ Vị trí ghế 🧔 Ba (Rower) đã có người ngồi ({m_DadPlayer.name})!");
                    return;
                }
            }
            else
            {
                if (m_ChildPlayer != null)
                {
                    Debug.LogWarning($"[CoopWoodenBoat] ⚠️ Vị trí ghế 👶 Con (Navigator) đã có người ngồi ({m_ChildPlayer.name})!");
                    return;
                }
            }

            // 5. Lên thuyền (hỗ trợ cả Network RPC cho Client lẫn Server/Local)
            if (IsSpawned && !IsServer)
            {
                var netObj = localPlayer.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    Debug.Log($"[CoopWoodenBoat] 📡 Gửi yêu cầu lên thuyền tới Host (Client -> ServerRpc)...");
                    RequestMountServerRpc(netObj, isDad);
                }
                else
                {
                    MountPlayer(localPlayer, isDad);
                }
            }
            else
            {
                Debug.Log($"[CoopWoodenBoat] ✅ Lên thuyền thành công! Nhân vật '{localPlayer.name}' vào vị trí {(isDad ? "🧔 Ba" : "👶 Con")}.");
                MountPlayer(localPlayer, isDad);
                if (CanWriteNetVars)
                {
                    var netObj = localPlayer.GetComponent<NetworkObject>();
                    if (netObj != null)
                    {
                        if (isDad && m_NetDadPlayer.CanClientWrite(NetworkManager.LocalClientId)) m_NetDadPlayer.Value = netObj;
                        else if (m_NetChildPlayer.CanClientWrite(NetworkManager.LocalClientId)) m_NetChildPlayer.Value = netObj;
                    }
                }
                CheckAndMountCompanionDummy();
            }
        }

        /// <summary>
        /// Đưa người chơi vào ghế ngồi trên thuyền và cố định vị trí ngay lập tức.
        /// </summary>
        public void ResetForCheckpoint(Vector3 position)
        {
            DismountPlayer(true); DismountPlayer(false);
            m_CurrentSpeed = 0; m_ExternalDrift = Vector3.zero;
            m_CurrentTillerAngle = 0; m_ChildSteerInput = 0;
            m_DashUntil = 0; m_NextDash = 0; m_LastStrokeAt = -100;
            m_IsGroundedOnLand = false; m_IsDadRowing = false; m_IsStrokeActive = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(-90, 180, 0));
            if (CanWriteNetVars)
            {
                if (m_NetDadPlayer.CanClientWrite(NetworkManager.LocalClientId)) m_NetDadPlayer.Value = default;
                if (m_NetChildPlayer.CanClientWrite(NetworkManager.LocalClientId)) m_NetChildPlayer.Value = default;
                m_NetBoatPosition.Value = position; m_NetBoatRotation.Value = transform.rotation;
                m_NetSpeed.Value = 0; m_NetTiller.Value = 0;
            }
        }
        public void MountCrewAtCheckpoint()
        {
            if (!CanWriteNetVars) return;
            foreach (var actor in LevelTwoActors.All)
            {
                bool dad = LevelOnePlayer.IsDad(actor);
                MountPlayer(actor.gameObject, dad);
                if (dad)
                {
                    if (m_NetDadPlayer.CanClientWrite(NetworkManager.LocalClientId)) m_NetDadPlayer.Value = actor.NetworkObject;
                }
                else
                {
                    if (m_NetChildPlayer.CanClientWrite(NetworkManager.LocalClientId)) m_NetChildPlayer.Value = actor.NetworkObject;
                }
            }
        }

        public void MountPlayer(GameObject player, bool asDad)
        {
            if (player == null) return;

            var cc = player.GetComponent<CharacterController>();
            var motor = player.GetComponent<CoreMovement>();
            var anim = player.GetComponentInChildren<Animator>();

            if (asDad)
            {
                m_DadPlayer = player;
                m_DadCC = cc;
                m_DadMovement = motor;
                m_DadAnimator = anim;
            }
            else
            {
                m_ChildPlayer = player;
                m_ChildCC = cc;
                m_ChildMovement = motor;
                m_ChildAnimator = anim;
            }

            // Tắt CharacterController và ngắt input di chuyển chân
            if (cc != null) cc.enabled = false;
            if (motor != null)
            {
                motor.SetMoveInput(Vector2.zero);
                motor.IsMovementEnabled = false;
            }

            // Khóa vị trí nhân vật ngay lập tức vào ghế ngồi
            Transform mountPoint = asDad ? (rowerMountPoint ?? seatRower) : (navigatorMountPoint ?? seatNavigator);
            Vector3 seatOffset = asDad ? dadSeatLocalOffset : childSeatLocalOffset;
            Vector3 bowDir = GetBoatWorldForward();
            Quaternion facingRot = Quaternion.LookRotation(bowDir, Vector3.up);

            Vector3 seatPos = mountPoint != null 
                ? mountPoint.position + seatOffset 
                : transform.position + (asDad ? bowDir * 0.5f : -bowDir * 1.2f) + seatOffset;

            player.transform.position = seatPos;
            player.transform.rotation = facingRot;

            if (motor != null && motor.IsOwner)
            {
                motor.SetPosition(seatPos, false);
            }

            // Kích hoạt tư thế ngồi
            if (anim != null)
            {
                anim.SetBool(s_PiggybackingHash, true);
                anim.SetBool(s_SittingHash, true);
                anim.CrossFadeInFixedTime("Piggyback_Sit", 0.15f);
            }

            Debug.Log($"[CoopWoodenBoat] 🪑 Đã cố định nhân vật '{player.name}' vào ghế {(asDad ? "🧔 Ba" : "👶 Con")}!");
        }

        /// <summary>
        /// Cho người chơi rời khỏi thuyền và đứng trên bờ an toàn.
        /// </summary>
        public void DismountPlayer(bool isDad)
        {
            GameObject player = isDad ? m_DadPlayer : m_ChildPlayer;
            if (player == null) return;

            var cc = isDad ? m_DadCC : m_ChildCC;
            var motor = isDad ? m_DadMovement : m_ChildMovement;
            var anim = isDad ? m_DadAnimator : m_ChildAnimator;

            // Đặt người chơi xuống mạn thuyền bên phải an toàn (hướng cầu tàu)
            Vector3 boatRight = GetBoatWorldRight();
            Vector3 safePos = transform.position + boatRight * 2.0f + Vector3.up * 0.5f;
            Vector3 dockPosition = default;
            bool atDock = riverPhysicsEnabled && LevelTwoDock.TryLanding(transform.position, isDad, out dockPosition);
            if (atDock) safePos = dockPosition;

            // Kiểm tra raycast xuống sàn cầu tàu hoặc đất liền
            if (!atDock && Physics.Raycast(safePos + Vector3.up * 2f, Vector3.down, out var groundHit, 5f, ~0, QueryTriggerInteraction.Ignore))
            {
                safePos = groundHit.point + Vector3.up * 0.1f;
            }

            player.transform.position = safePos;

            if (cc != null) cc.enabled = true;
            if (motor != null)
            {
                motor.IsMovementEnabled = true;
                if (motor.IsOwner) motor.SetPosition(safePos, false);
            }

            if (anim != null)
            {
                anim.SetBool(s_PiggybackingHash, false);
                anim.SetBool(s_SittingHash, false);
                anim.CrossFadeInFixedTime("Idle Walk Run Blend", 0.15f);
            }

            if (isDad)
            {
                m_DadPlayer = null;
                m_DadCC = null;
                m_DadMovement = null;
                m_DadAnimator = null;
                m_IsStrokeActive = false;
                m_StrokePhase = 1f;
                m_IsDadRowing = false;
            }
            else
            {
                m_ChildPlayer = null;
                m_ChildCC = null;
                m_ChildMovement = null;
                m_ChildAnimator = null;
                m_ChildSteerInput = 0f;
            }

            Debug.Log($"[CoopWoodenBoat] ⚓ Người chơi {(isDad ? "🧔 Ba" : "👶 Con")} đã xuống thuyền an toàn tại {safePos}.");

            // Nếu người còn lại trên thuyền là SoloCompanionDummy, cho dummy xuống cùng người chơi
            GameObject other = isDad ? m_ChildPlayer : m_DadPlayer;
            if (other != null && other.GetComponent<SoloCompanionDummy>() != null)
            {
                Debug.Log($"[CoopWoodenBoat] 🤖 Tự động cho Bạn Đồng Hành ({other.name}) cùng xuống thuyền theo người chơi.");
                DismountPlayer(!isDad);
            }
        }

        private void CheckAndMountCompanionDummy()
        {
            var dummy = FindAnyObjectByType<SoloCompanionDummy>();
            if (dummy == null) return;

            // Nếu Ba đã lên và Con còn trống -> Dummy lên ghế Con
            if (m_DadPlayer != null && m_ChildPlayer == null)
            {
                Debug.Log($"[CoopWoodenBoat] 🤖 Tự động đưa Bạn Đồng Hành ({dummy.name}) lên ghế 👶 Con để hỗ trợ bẻ lái!");
                MountPlayer(dummy.gameObject, false);
            }
            // Nếu Con đã lên và Ba còn trống -> Dummy lên ghế Ba
            else if (m_ChildPlayer != null && m_DadPlayer == null)
            {
                Debug.Log($"[CoopWoodenBoat] 🤖 Tự động đưa Bạn Đồng Hành ({dummy.name}) lên ghế 🧔 Ba để hỗ trợ chèo thuyền!");
                MountPlayer(dummy.gameObject, true);
            }
        }

        #endregion

        #region Controls & Physics Simulation

        /// <summary>
        /// Đồng bộ mượt mà vị trí, góc quay và vận tốc từ Server xuống Client.
        /// </summary>
        private void SyncTransformFromNetwork()
        {
            if (m_NetBoatPosition.Value == Vector3.zero) return;

            float dist = Vector3.Distance(transform.position, m_NetBoatPosition.Value);
            if (dist > 5.0f)
            {
                // Nhảy vị trí ngay nếu lệch quá xa (vừa vào game hoặc teleport)
                transform.position = m_NetBoatPosition.Value;
                transform.rotation = m_NetBoatRotation.Value;
            }
            else
            {
                // Nội suy mượt mà theo vị trí từ Server
                transform.position = Vector3.Lerp(transform.position, m_NetBoatPosition.Value, Time.deltaTime * 20f);
                transform.rotation = Quaternion.Slerp(transform.rotation, m_NetBoatRotation.Value, Time.deltaTime * 20f);
            }

            m_CurrentSpeed = Mathf.Lerp(m_CurrentSpeed, m_NetSpeed.Value, Time.deltaTime * 12f);

            var localPlayer = FindLocalPlayer();
            bool isMeChild = m_ChildPlayer != null && m_ChildPlayer == localPlayer;
            // Nếu không phải là Con đang trực tiếp bẻ lái thì mới lấy góc từ Network (tránh giật cục input của Con)
            if (!isMeChild)
            {
                m_CurrentTillerAngle = Mathf.Lerp(m_CurrentTillerAngle, m_NetTiller.Value, Time.deltaTime * 15f);
            }

            m_IsDadRowing = m_NetIsRowing.Value;
            m_IsGroundedOnLand = m_NetIsGrounded.Value;
        }

        private void OnNetDadPlayerChanged(NetworkObjectReference oldRef, NetworkObjectReference newRef)
        {
            if (newRef.TryGet(out NetworkObject netObj))
            {
                MountPlayer(netObj.gameObject, true);
            }
            else
            {
                DismountPlayer(true);
            }
        }

        private void OnNetChildPlayerChanged(NetworkObjectReference oldRef, NetworkObjectReference newRef)
        {
            if (newRef.TryGet(out NetworkObject netObj))
            {
                MountPlayer(netObj.gameObject, false);
            }
            else
            {
                DismountPlayer(false);
            }
        }

        /// <summary>
        /// Đọc phím điều khiển từ người chơi.
        /// - 🧔 Ba: Nhấn phím [SPACE] để chèo thuyền tiến về phía trước (mỗi nhịp tiến 1m, spam càng nhanh chèo càng nhanh).
        /// - 👶 Con: Phím [A] / [D] bẻ cần lái (Cần qua Phải -> Rẽ Trái, Cần qua Trái -> Rẽ Phải).
        /// </summary>
        private void ReadMountedPlayerControls()
        {
            // Nếu KHÔNG CÓ AI trên thuyền: thuyền đứng yên
            if (m_DadPlayer == null && m_ChildPlayer == null)
            {
                m_IsDadRowing = false;
                m_IsStrokeActive = false;
                m_StrokePhase = 1f;
                m_ChildSteerInput = 0f;
                m_CurrentSpeed = Mathf.MoveTowards(m_CurrentSpeed, 0f, waterDrag * Time.deltaTime * 4f);
                m_CurrentTillerAngle = Mathf.MoveTowards(m_CurrentTillerAngle, 0f, tillerTurnSpeed * Time.deltaTime);
                return;
            }

            // Kiểm tra xem người chơi cục bộ có ĐANG NGỒI TRÊN THUYỀN hay không
            var localPlayer = FindLocalPlayer();
            bool isMeDad = m_DadPlayer != null && m_DadPlayer == localPlayer;
            bool isMeChild = m_ChildPlayer != null && m_ChildPlayer == localPlayer;

            // Nếu người chơi cục bộ không ngồi trên thuyền: bỏ qua
            if (!isMeDad && !isMeChild)
            {
                return;
            }

            // 1. Nhận diện phím [SPACE] để chèo thuyền (Dad)
            bool spacePressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame))
            {
                spacePressed = true;
            }
            if (!spacePressed && Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                spacePressed = true;
            }
#else
            if (!spacePressed && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow)))
            {
                spacePressed = true;
            }
#endif

            // 2. Nhận diện phím [A] / [D] để bẻ lái (Child)
            float steerInput = 0f;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) steerInput += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) steerInput -= 1f;
            }
            if (Gamepad.current != null)
            {
                float stickX = Gamepad.current.leftStick.x.ReadValue();
                if (Mathf.Abs(stickX) > 0.15f) steerInput += stickX;
            }
#else
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) steerInput += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) steerInput -= 1f;
#endif
            steerInput = Mathf.Clamp(steerInput, -1f, 1f);

            // 3. Phân quyền vai trò nghiêm ngặt:
            if (isMeDad)
            {
                // Ba CHỈ chèo thuyền bằng phím Space
                if (spacePressed)
                {
                    if (m_IsGroundedOnLand)
                    {
                        Debug.LogWarning("[CoopWoodenBoat] ⚠️ Thuyền đang mắc cạn trên bờ đất! Không thể chèo tiếp dưới cạn.");
                    }
                    else
                    {
                        // Client prediction visual tức thì cho người chèo
                        m_IsStrokeActive = true;
                        m_StrokePhase = 0f;
                        m_IsDadRowing = true;

                        if (IsSpawned && !IsServer)
                        {
                            SendRowStrokeServerRpc();
                        }
                        else
                        {
                            TriggerRowStroke();
                        }
                    }
                }

                // Ba KHÔNG được điều khiển cần lái khi đã có Con trên thuyền
                // Chỉ cho phép lái khi ở chế độ Solo thử nghiệm và Con chưa lên thuyền
                if (soloTestMode && m_ChildPlayer == null)
                {
                    m_ChildSteerInput = steerInput;
                    if (IsSpawned && !IsServer)
                    {
                        SendSteerInputServerRpc(m_ChildSteerInput);
                    }
                }
            }
            else if (isMeChild)
            {
                // Con là người DUY NHẤT bẻ cần lái
                m_ChildSteerInput = steerInput;
                if (IsSpawned && !IsServer)
                {
                    SendSteerInputServerRpc(m_ChildSteerInput);
                }

                // Client prediction góc cần lái để Con thấy phản hồi mượt mà ngay lập tức
                float targetAngle = m_ChildSteerInput * maxTillerAngle;
                m_CurrentTillerAngle = Mathf.MoveTowards(m_CurrentTillerAngle, targetAngle, tillerTurnSpeed * Time.deltaTime);

                // Chế độ solo: nếu Ba chưa lên thuyền thì Con mới chèo tạm để kiểm thử
                if (soloTestMode && m_DadPlayer == null)
                {
                    if (spacePressed)
                    {
                        if (m_IsGroundedOnLand)
                        {
                            Debug.LogWarning("[CoopWoodenBoat] ⚠️ Thuyền đang mắc cạn trên bờ đất! Không thể chèo tiếp dưới cạn.");
                        }
                        else
                        {
                            m_IsStrokeActive = true;
                            m_StrokePhase = 0f;
                            m_IsDadRowing = true;

                            if (IsSpawned && !IsServer)
                            {
                                SendRowStrokeServerRpc();
                            }
                            else
                            {
                                TriggerRowStroke();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Kích hoạt một nhịp chèo bằng phím Space:
        /// - Nhấn Space 1 lần -> Tăng tốc độ tức thì cho thuyền.
        /// - Càng spam Space nhanh -> Vận tốc thuyền tăng cao (lên tối đa maxRowSpeed).
        /// - Khi dừng Space -> Thuyền tiếp tục lướt trôi theo quán tính tốc độ hiện tại.
        /// - Nếu thuyền đã mắc cạn trên bờ đất -> Không thể di chuyển được nữa!
        /// </summary>
        public void TriggerRowStroke()
        {
            if (!HasSimulationAuthority) return;
            m_LastStrokeAt = Time.time;
            // Kiểm tra trạng thái mắc cạn trên đất liền:
            if (m_IsGroundedOnLand)
            {
                Debug.LogWarning("[CoopWoodenBoat] ⚠️ Thuyền đã lên bờ đất (mắc cạn)! Thuyền chỉ di chuyển được dưới nước, không thể chèo trên cạn.");
                return;
            }

            // Kích hoạt nhịp chèo mới tức thì
            m_IsStrokeActive = true;
            m_StrokePhase = 0f;
            m_IsDadRowing = true;

            // Tăng tốc độ: mỗi lần nhấn Space tăng thêm vận tốc tức thì
            if (m_CurrentSpeed < minStrokeSpeed)
            {
                m_CurrentSpeed = minStrokeSpeed;
            }
            else
            {
                m_CurrentSpeed = Mathf.Min(m_CurrentSpeed + speedBoostPerSpam, maxRowSpeed);
            }

            if (CanWriteNetVars)
            {
                m_NetIsRowing.Value = true;
                m_NetSpeed.Value = m_CurrentSpeed;
                NotifyRowStrokeClientRpc();
            }
        }

        /// <summary>
        /// Đo cao độ mặt đất / đáy sông tại tọa độ bất kỳ (kết hợp Raycast và Terrain).
        /// </summary>
        public float GetGroundHeight(Vector3 worldPos)
        {
            Vector3 rayOrigin = new Vector3(worldPos.x, waterSurfaceY + 15f, worldPos.z);
            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 35f, ~0, QueryTriggerInteraction.Ignore);
            
            float highestGroundY = float.MinValue;
            bool hitFound = false;

            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i].collider;
                if (col == null || col.isTrigger) continue;
                if (col == m_BoatCollider || col.gameObject == gameObject || col.transform.IsChildOf(transform)) continue;
                // Bridges, gates and seated players are obstacles, never the river bed.
                if (riverPhysicsEnabled && !(col is TerrainCollider)) continue;

                if (hits[i].point.y > highestGroundY)
                {
                    highestGroundY = hits[i].point.y;
                    hitFound = true;
                }
            }

            if (hitFound)
            {
                return highestGroundY;
            }

            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                return terrain.SampleHeight(worldPos) + terrain.transform.position.y;
            }

            return 0f;
        }

        /// <summary>
        /// Ước tính pháp tuyến dốc bờ sông (Shore Normal) tại điểm kiểm tra.
        /// </summary>
        private Vector3 GetShoreGradientNormal(Vector3 pos)
        {
            float step = 1.0f;
            float hL = GetGroundHeight(pos - Vector3.right * step);
            float hR = GetGroundHeight(pos + Vector3.right * step);
            float hB = GetGroundHeight(pos - Vector3.forward * step);
            float hF = GetGroundHeight(pos + Vector3.forward * step);

            // Vector trỏ từ bờ cao xuống nước sâu
            Vector3 normal = new Vector3(hL - hR, 2.0f, hB - hF).normalized;
            return normal;
        }

        /// <summary>
        /// Cập nhật trạng thái mắc cạn của thuyền trên bờ đất.
        /// Thuyền bị mắc cạn khi thân thuyền nằm trên mặt đất khô (cao độ đất >= mặt nước - 0.04m).
        /// </summary>
        private void UpdateGroundedStatus()
        {
            float groundY = GetGroundHeight(transform.position);
            
            bool wasGrounded = m_IsGroundedOnLand;
            m_IsGroundedOnLand = groundY >= (waterSurfaceY - 0.04f);

            if (m_IsGroundedOnLand)
            {
                // Triệt tiêu vận tốc ngay khi thuyền đã lên hẳn bờ đất
                m_CurrentSpeed = 0f;
                m_IsDadRowing = false;
                m_IsStrokeActive = false;
                m_StrokePhase = 1f;

                if (!wasGrounded)
                {
                    Debug.LogWarning($"[CoopWoodenBoat] ⚠️ Thuyền đã lên bờ đất tại {transform.position} (Cao độ đất: {groundY:F2}m >= Mặt nước: {waterSurfaceY:F2}m) -> MẮC CẠN! Không thể di chuyển tiếp.");
                }
            }
        }

        /// <summary>
        /// Mô phỏng di chuyển thẳng của thuyền và góc bẻ lái ngược hướng.
        /// </summary>
        private void SimulateBoatPhysics()
        {
            float dt = Time.deltaTime;
            if (riverPhysicsEnabled && !HasCoopCrew) m_ExternalDrift = Vector3.zero;
            if (riverPhysicsEnabled && Time.time < m_DashUntil && HasCoopCrew) m_CurrentSpeed = Mathf.Max(m_CurrentSpeed, 11f);
            Vector3 forwardDir = GetBoatWorldForward();

            // 0. CẬP NHẬT TRẠNG THÁI MẮC CẠN
            UpdateGroundedStatus();

            // 1. TÍNH TOÁN BẺ LÁI (CHILD STEERING)
            if (!IsSpawned || IsServer)
            {
                float targetTillerAngle = m_ChildSteerInput * maxTillerAngle;
                m_CurrentTillerAngle = Mathf.MoveTowards(m_CurrentTillerAngle, targetTillerAngle, tillerTurnSpeed * dt);
            }

            // Nếu không bị mắc cạn trên đất và có góc cần lái -> Quay thuyền
            if (!m_IsGroundedOnLand && Mathf.Abs(m_CurrentTillerAngle) > 0.1f)
            {
                float tillerRatio = m_CurrentTillerAngle / maxTillerAngle;
                float turnDirection = invertTillerSteering ? -tillerRatio : tillerRatio;

                // Thuyền chuyển hướng nhạy khi đang có đà lướt nước
                float speedFactor = Mathf.Clamp01(Mathf.Abs(m_CurrentSpeed) / maxRowSpeed);
                float effectiveTurnRate = boatTurnSensitivity * (0.35f + 0.65f * speedFactor);

                float yawDelta = turnDirection * effectiveTurnRate * dt;
                transform.Rotate(Vector3.up, yawDelta, Space.World);
            }

            // 2. TÍNH TOÁN TIẾN THẲNG THEO NHỊP CHÈO SPACE VÀ TRÔI QUÁN TÍNH
            // THUYỀN CHỈ DI CHUYỂN ĐƯỢC KHI Ở DƯỚI MẶT NƯỚC (KHÔNG BỊ MẮC CẠN TRÊN ĐẤT)
            if (!m_IsGroundedOnLand && (m_CurrentSpeed > 0.02f || m_ExternalDrift.sqrMagnitude > 0.0001f))
            {
                float moveDist = m_CurrentSpeed * dt;
                Vector3 desiredMove = forwardDir * moveDist + m_ExternalDrift * dt;
                m_ExternalDrift *= Mathf.Exp(-0.65f * dt);

                // Quét va chạm chướng ngại vật & mép bờ sông để ngăn phi thuyền lên đất liền
                if (CheckObstacleAndShoreCollision(desiredMove, out Vector3 safeMove))
                {
                    transform.position += safeMove;
                }
                else
                {
                    transform.position += desiredMove;
                }

                // Cản nước làm giảm tốc độ
                float currentDrag = m_IsStrokeActive ? (waterDrag * 0.35f) : waterDrag;
                m_CurrentSpeed = Mathf.MoveTowards(m_CurrentSpeed, 0f, currentDrag * dt);

                if (m_CurrentSpeed <= 0.05f)
                {
                    m_CurrentSpeed = 0f;
                    m_IsDadRowing = false;
                }
            }
            else
            {
                m_CurrentSpeed = 0f;
                m_IsDadRowing = false;
                m_ExternalDrift = Vector3.zero;
            }
        }

        /// <summary>
        /// Quét vật cản và mép bờ sông để:
        /// 1. Ngăn thuyền leo lên mặt đất khô (chỉ cho thuyền di chuyển trong lòng sông).
        /// 2. Ngăn thuyền xuyên thủng bờ đá, cọc cầu tàu, vật cản cứng.
        /// </summary>
        private bool CheckObstacleAndShoreCollision(Vector3 moveDelta, out Vector3 safeMove)
        {
            safeMove = moveDelta;
            if (riverPhysicsEnabled && m_BoatCollider is BoxCollider hull && moveDelta.sqrMagnitude > 0.000001f)
            {
                var half = Vector3.Scale(hull.size * 0.5f, transform.lossyScale);
                var hits = Physics.BoxCastAll(transform.TransformPoint(hull.center), half,
                    moveDelta.normalized, transform.rotation, moveDelta.magnitude + 0.04f, ~0, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity;
                RaycastHit obstacle = default;
                foreach (var candidate in hits)
                {
                    var col = candidate.collider;
                    if (col == hull || col.transform.IsChildOf(transform) || col is TerrainCollider ||
                        col.GetComponentInParent<CorePlayerManager>() != null) continue;
                    if (candidate.distance < nearest) { nearest = candidate.distance; obstacle = candidate; }
                }
                if (nearest < float.PositiveInfinity)
                {
                    safeMove = moveDelta.normalized * Mathf.Max(0f, nearest - 0.04f);
                    ApplyRockImpact(obstacle.normal);
                    return true;
                }
            }
            Vector3 forwardDir = GetBoatWorldForward();
            Vector3 rightDir = GetBoatWorldRight();

            // 1. KIỂM TRA MẶT ĐẤT / BỜ SÔNG PHÍA TRƯỚC (SHORELINE BOUNDARY)
            Vector3 nextPos = transform.position + moveDelta;
            Vector3 nextBow = nextPos + forwardDir * 2.1f;
            Vector3 nextLeft = nextPos - rightDir * 0.75f;
            Vector3 nextRight = nextPos + rightDir * 0.75f;

            float bowGroundY = GetGroundHeight(nextBow);
            float centerGroundY = GetGroundHeight(nextPos);
            float leftGroundY = GetGroundHeight(nextLeft);
            float rightGroundY = GetGroundHeight(nextRight);

            // Ngưỡng bờ đất: Nếu cao độ đất >= (waterSurfaceY - 0.08m) -> Đó là mép bờ cạn / đất liền
            float maxGroundAhead = Mathf.Max(bowGroundY, Mathf.Max(centerGroundY, Mathf.Max(leftGroundY, rightGroundY)));
            float currentCenterGround = GetGroundHeight(transform.position);

            // Nếu vị trí dự kiến tiếp theo sẽ đưa mũi hoặc thân thuyền leo lên bờ đất
            if (maxGroundAhead >= (waterSurfaceY - 0.08f))
            {
                // Nếu đang hướng về phía đất cao hơn hiện tại
                if (maxGroundAhead > currentCenterGround + 0.02f)
                {
                    // Lấy hướng tiếp tuyến chạy dọc mép bờ sông (để thuyền có thể trượt ven bờ nếu đi chéo)
                    Vector3 shoreNormal = GetShoreGradientNormal(nextBow);
                    Vector3 slideVector = Vector3.ProjectOnPlane(moveDelta, shoreNormal);
                    slideVector.y = 0f;

                    // Kiểm tra xem di chuyển theo hướng trượt ven bờ có bị leo lên đất nữa không
                    Vector3 slideBow = transform.position + slideVector + forwardDir * 2.1f;
                    if (GetGroundHeight(slideBow) < (waterSurfaceY - 0.08f))
                    {
                        safeMove = slideVector * 0.5f;
                        m_CurrentSpeed *= 0.5f;
                    }
                    else
                    {
                        // Hướng thẳng vào bờ đất: Chặn đứng hoàn toàn! Thuyền dừng ngay mép nước
                        safeMove = Vector3.zero;
                        m_CurrentSpeed = 0f;
                    }

                    return true;
                }
            }

            // 2. KIỂM TRA VẬT CẢN VẬT LÝ CỨNG (ROCKS, DOCK PILLARS, BARRIERS)
            float checkDist = moveDelta.magnitude + 0.4f;
            Vector3 checkOrigin = transform.position + Vector3.up * 0.3f;

            if (!riverPhysicsEnabled && Physics.SphereCast(checkOrigin, 0.7f, moveDelta.normalized, out var hit, checkDist))
            {
                if (!hit.collider.isTrigger && hit.collider != m_BoatCollider && !hit.collider.transform.IsChildOf(transform))
                {
                    safeMove = Vector3.ProjectOnPlane(moveDelta, hit.normal);
                    safeMove.y = 0f;
                    m_CurrentSpeed *= 0.5f;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tạo hiệu ứng bập bênh mặt nước sống động khi ở dưới nước, hoặc nằm vững trên mặt đất khi mắc cạn.
        /// </summary>
        private void UpdateBuoyancyBobbing()
        {
            float groundY = GetGroundHeight(transform.position);

            if (m_IsGroundedOnLand)
            {
                // Thuyền nằm cố định vững vàng trên mặt đất khô ráo
                float targetY = groundY + restingHeightOffset;
                Vector3 curPos = transform.position;
                transform.position = new Vector3(curPos.x, targetY, curPos.z);
            }
            else
            {
                // Thuyền nổi bập bênh theo sóng nước
                float waveY = Mathf.Sin(Time.time * bobbingFrequency) * bobbingAmplitude;
                float targetY = waterSurfaceY + restingHeightOffset + waveY;

                // Đảm bảo không bị cắm đáy xuyên thủng lòng sông ở vùng nước nông
                float minGroundY = groundY + restingHeightOffset;
                targetY = Mathf.Max(targetY, minGroundY);

                Vector3 curPos = transform.position;
                transform.position = new Vector3(curPos.x, targetY, curPos.z);
            }
        }

        /// <summary>
        /// Visual Animation: Xoay mái chèo theo nhịp chèo của Ba và xoay cần lái theo tay Con.
        /// Chạy trên cả Host và Client để Con luôn thấy hoạt ảnh mái chèo khi Ba chèo.
        /// </summary>
        private void AnimateOarsAndRudder()
        {
            // 1. CẦN LÁI (RUDDER ASSEMBLY)
            // Xoay cần lái quanh trục thẳng đứng Z trong tọa độ Blender FBX
            if (rudderAssembly != null)
            {
                rudderAssembly.localEulerAngles = new Vector3(0f, 0f, m_CurrentTillerAngle);
            }

            // 2. MÁI CHÈO (OAR LEFT & RIGHT)
            if (oarLeft != null && oarRight != null)
            {
                // Chỉ chạy hoạt ảnh chèo khi ĐANG TRONG NHỊP CHÈO (m_IsStrokeActive)
                // Khi người chơi không bấm Space nữa -> nhịp kết thúc -> mái chèo đứng yên ở vị trí nghỉ!
                if (m_IsStrokeActive && m_StrokePhase < 1f)
                {
                    float sweep;
                    float dip;

                    if (m_StrokePhase < 0.5f)
                    {
                        // Giai đoạn 1 (0 -> 0.5): Kéo mái chèo về sau và cắm sâu xuống nước
                        float t1 = m_StrokePhase * 2f; // 0 -> 1
                        sweep = Mathf.Sin(t1 * Mathf.PI * 0.5f) * oarStrokeAngle; // 0 -> oarStrokeAngle
                        dip = Mathf.Sin(t1 * Mathf.PI) * oarDipAngle;             // Cắm xuống nước
                    }
                    else
                    {
                        // Giai đoạn 2 (0.5 -> 1.0): Nâng mái chèo lên khỏi mặt nước và đưa về vị trí nghỉ
                        float t2 = (m_StrokePhase - 0.5f) * 2f; // 0 -> 1
                        sweep = Mathf.Cos(t2 * Mathf.PI * 0.5f) * oarStrokeAngle;  // oarStrokeAngle -> 0
                        dip = -Mathf.Sin(t2 * Mathf.PI) * (oarDipAngle * 0.4f);    // Nhấc cao khỏi mặt nước
                    }

                    // Mái chèo quét tới lui đối xứng và nghiêng theo góc cắm nước
                    Quaternion strokeL = Quaternion.AngleAxis(sweep, Vector3.forward) * Quaternion.AngleAxis(-dip, Vector3.right);
                    Quaternion strokeR = Quaternion.AngleAxis(-sweep, Vector3.forward) * Quaternion.AngleAxis(-dip, Vector3.right);

                    oarLeft.localRotation = strokeL * m_OarLeftInitialRot;
                    oarRight.localRotation = strokeR * m_OarRightInitialRot;
                }
                else
                {
                    // Trả mái chèo về trạng thái nghỉ tự nhiên và giữ đứng yên hoàn toàn
                    oarLeft.localRotation = Quaternion.Slerp(oarLeft.localRotation, m_OarLeftInitialRot, Time.deltaTime * 10f);
                    oarRight.localRotation = Quaternion.Slerp(oarRight.localRotation, m_OarRightInitialRot, Time.deltaTime * 10f);
                }
            }
        }

        /// <summary>
        /// Giữ chặt người chơi tại ghế ngồi khi thuyền di chuyển.
        /// </summary>
        private void StickPlayersToSeats()
        {
            Vector3 bowDir = GetBoatWorldForward();
            Quaternion facingRot = Quaternion.LookRotation(bowDir, Vector3.up);

            if (m_DadPlayer != null)
            {
                Vector3 seatPos = (rowerMountPoint != null ? rowerMountPoint.position : transform.position + bowDir * 0.5f) + dadSeatLocalOffset;
                m_DadPlayer.transform.position = seatPos;
                m_DadPlayer.transform.rotation = facingRot;
            }

            if (m_ChildPlayer != null)
            {
                Vector3 seatPos = (navigatorMountPoint != null ? navigatorMountPoint.position : transform.position - bowDir * 1.2f) + childSeatLocalOffset;
                m_ChildPlayer.transform.position = seatPos;
                m_ChildPlayer.transform.rotation = facingRot;
            }
        }

        #endregion

        #region Helpers & Coordinate Transformations

        /// <summary>
        /// Tính khoảng cách ngắn nhất từ người chơi tới thân thuyền, mạn thuyền hoặc 2 ghế ngồi.
        /// Giúp người chơi đứng ở bất kỳ vị trí nào trên bến tàu cạnh thuyền cũng nhấn [E] được.
        /// </summary>
        public float GetDistanceToPlayer(Vector3 playerPos)
        {
            float minDist = Vector3.Distance(transform.position, playerPos);

            if (rowerMountPoint != null)
            {
                float d = Vector3.Distance(rowerMountPoint.position, playerPos);
                if (d < minDist) minDist = d;
            }

            if (navigatorMountPoint != null)
            {
                float d = Vector3.Distance(navigatorMountPoint.position, playerPos);
                if (d < minDist) minDist = d;
            }

            if (m_BoatCollider != null)
            {
                Vector3 closest = m_BoatCollider.ClosestPoint(playerPos);
                float d = Vector3.Distance(closest, playerPos);
                if (d < minDist) minDist = d;
            }

            return minDist;
        }

        /// <summary>
        /// Hướng mũi thuyền trong không gian thế giới (World Forward).
        /// Do FBX Blender có trục Y hướng về mũi thuyền (Local Y = Bow).
        /// </summary>
        public Vector3 GetBoatWorldForward()
        {
            Vector3 fwd = transform.TransformDirection(new Vector3(0f, 1f, 0f));
            fwd.y = 0f;
            return fwd.normalized;
        }

        /// <summary>
        /// Hướng mạn phải thuyền trong không gian thế giới (World Right).
        /// </summary>
        public Vector3 GetBoatWorldRight()
        {
            Vector3 bow = GetBoatWorldForward();
            return Vector3.Cross(Vector3.up, bow).normalized;
        }

        private GameObject FindLocalPlayer()
        {
            // 1. Nếu có NetworkManager, lấy local player từ LocalClient
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
            {
                var localNetObj = NetworkManager.Singleton.LocalClient.PlayerObject;
                if (localNetObj != null) return localNetObj.gameObject;
            }

            // 2. Tìm nhân vật có CoreCameraController ĐANG ENABLED và ACTIVE (không phải dummy)
            var camControllers = FindObjectsByType<CoreCameraController>();
            foreach (var cam in camControllers)
            {
                if (cam.enabled && cam.gameObject.activeInHierarchy && cam.GetComponent<SoloCompanionDummy>() == null)
                {
                    return cam.gameObject;
                }
            }

            // 3. Tìm CoreMovement có IsOwner hoặc active (không phải dummy)
            var movements = FindObjectsByType<CoreMovement>();
            foreach (var m in movements)
            {
                if (m.gameObject.activeInHierarchy && m.GetComponent<SoloCompanionDummy>() == null)
                {
                    if (m.TryGetComponent<NetworkObject>(out var netObj))
                    {
                        if (netObj.IsOwner) return m.gameObject;
                    }
                    else
                    {
                        return m.gameObject;
                    }
                }
            }

            // 4. Fallback: Bất kỳ CoreMovement nào không phải Dummy
            foreach (var m in movements)
            {
                if (m.GetComponent<SoloCompanionDummy>() == null)
                {
                    return m.gameObject;
                }
            }

            return null;
        }

        private bool IsPlayerDad(GameObject player)
        {
            if (player == null) return false;

            if (player.TryGetComponent<PlayerRoleVisuals>(out var visuals))
            {
                return visuals.IsDad;
            }

            if (player.TryGetComponent<CoopBoostThrowAbility>(out var boost))
            {
                return boost.IsDad;
            }

            string lower = player.name.ToLower();
            if (lower.Contains("child") || lower.Contains("son") || lower.Contains("con")) return false;
            if (lower.Contains("dad") || lower.Contains("ba") || lower.Contains("father")) return true;

            var cc = player.GetComponent<CharacterController>();
            if (cc != null && cc.height < 1.45f) return false;

            return Lobby3DManager.AmIDad;
        }

        #endregion

        #region OnGUI Visual HUD & Prompts

        private void OnGUI()
        {
            if (TutorialIntroCutscene.IsAnyPlaying) return;
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null) return;

            bool isMeDadOnBoat = m_DadPlayer == localPlayer;
            bool isMeChildOnBoat = m_ChildPlayer == localPlayer;
            bool isMeOnBoat = isMeDadOnBoat || isMeChildOnBoat;

            float dist = GetDistanceToPlayer(localPlayer.transform.position);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!isMeOnBoat && dist > boardInteractionRadius)
            {
                GUIStyle debugStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
                debugStyle.normal.textColor = new Color(1f, 0.9f, 0.3f, 0.9f);
                GUI.Label(new Rect(20, 20, 450, 25), $"[Thuyền Co-op] Bấm phím [B] để dịch chuyển nhanh đến thuyền ({dist:F0}m)", debugStyle);
            }
#endif

            // 1. PROMPT KHI ĐỨNG GẦN THUYỀN (NEAR BOAT PROMPT)
            if (!isMeOnBoat && dist <= boardInteractionRadius)
            {
                bool isDad = IsPlayerDad(localPlayer);
                string seatName = isDad ? "🧔 Ba (Chèo thuyền - Phím Space)" : "👶 Con (Bẻ cần lái - Phím A/D)";
                bool isSeatFree = isDad ? (m_DadPlayer == null) : (m_ChildPlayer == null);

                GUIStyle promptStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                promptStyle.normal.textColor = Color.white;

                string text = isSeatFree
                    ? $"[E] LÊN THUYỀN\nVai trò: {seatName}"
                    : $"Vị trí {seatName} đã có người ngồi!";

                float boxW = 340f;
                float boxH = 55f;
                float boxX = (Screen.width - boxW) * 0.5f;
                float boxY = Screen.height - 130f;

                GUI.backgroundColor = new Color(0.1f, 0.15f, 0.25f, 0.88f);
                GUI.Box(new Rect(boxX, boxY, boxW, boxH), text, promptStyle);
            }

            // 2. HUD ĐIỀU KHIỂN KHI ĐANG NGỒI TRÊN THUYỀN (MOUNTED HUD)
            if (isMeOnBoat)
            {
                DrawMountedHUD(isMeDadOnBoat);
            }
        }

        private void DrawMountedHUD(bool isDad)
        {
            float hudW = 460f;
            float hudH = 135f;
            float hudX = (Screen.width - hudW) * 0.5f;
            float hudY = 25f;

            GUIStyle cardStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(16, 16, 12, 12)
            };
            GUI.backgroundColor = new Color(0.08f, 0.12f, 0.2f, 0.92f);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = new Color(1f, 0.88f, 0.35f);

            GUIStyle infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            infoStyle.normal.textColor = Color.white;

            GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleRight
            };
            statusStyle.normal.textColor = new Color(0.6f, 0.9f, 1f);

            GUI.Box(new Rect(hudX, hudY, hudW, hudH), GUIContent.none, cardStyle);

            string myRoleText = isDad ? "🧔 BA: ĐIỀU KHIỂN CHÈO THUYỀN (ROWING - SPACE)" : "👶 CON: ĐIỀU KHIỂN CẦN BẺ LÁI (TILLER - A/D)";
            GUI.Label(new Rect(hudX + 16, hudY + 10, hudW - 32, 26), myRoleText, titleStyle);

            string guide1 = isDad
                ? "• [SPACE] Nhấn để chèo tiến về phía trước (Mỗi nhịp = 1m)"
                : "• [A / D] Bẻ cần lái (Cần qua PHẢI -> Thuyền rẽ TRÁI, Cần qua TRÁI -> Thuyền rẽ PHẢI)";
            string guide2 = isDad
                ? "• Càng spam [SPACE] nhanh thuyền chèo càng nhanh! Thả phím thuyền sẽ trôi theo quán tính."
                : "• Giữ [A] hoặc [D] để bẻ lái, thả phím để cần tự trả về giữa. Ba phụ trách chèo!";
            string exitGuide = "• Nhấn [E] để XUỐNG THUYỀN";

            GUI.Label(new Rect(hudX + 16, hudY + 40, hudW - 32, 20), guide1, infoStyle);
            GUI.Label(new Rect(hudX + 16, hudY + 62, hudW - 32, 20), guide2, infoStyle);
            GUI.Label(new Rect(hudX + 16, hudY + 84, hudW - 32, 20), exitGuide, infoStyle);

            if (m_IsGroundedOnLand)
            {
                GUIStyle warningStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                warningStyle.normal.textColor = new Color(1f, 0.4f, 0.35f);
                GUI.Label(new Rect(hudX + 16, hudY + 108, hudW - 32, 20), "⚠️ THUYỀN ĐÃ MẮC CẠN TRÊN BỜ! (Không thể di chuyển - Nhấn [E] xuống thuyền)", warningStyle);
            }
            else
            {
                string rudderDirectionText = Mathf.Abs(m_CurrentTillerAngle) < 1.0f 
                    ? "Cần lái: GIỮA (Thẳng)"
                    : (m_CurrentTillerAngle > 0 ? $"Cần lái: PHẢI (+{m_CurrentTillerAngle:F0}°) -> Rẽ TRÁI ⬅" : $"Cần lái: TRÁI ({m_CurrentTillerAngle:F0}°) -> Rẽ PHẢI ➡");

                string statusText = $"Tốc độ: {m_CurrentSpeed:F1} m/s | {rudderDirectionText}";
                GUI.Label(new Rect(hudX + 16, hudY + 108, hudW - 32, 20), statusText, statusStyle);
            }
        }

        #endregion

        #region Network RPCs

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestMountServerRpc(NetworkObjectReference playerRef, bool asDad, RpcParams rpc = default)
        {
            if (playerRef.TryGet(out NetworkObject playerNet))
            {
                if (playerNet.OwnerClientId != rpc.Receive.SenderClientId || IsPlayerDad(playerNet.gameObject) != asDad) return;
                float dist = GetDistanceToPlayer(playerNet.transform.position);
                if (dist <= boardInteractionRadius + 2.5f)
                {
                    if (asDad)
                    {
                        if (m_NetDadPlayer.Value.TryGet(out _)) return;
                        if (m_NetDadPlayer.CanClientWrite(NetworkManager.LocalClientId)) m_NetDadPlayer.Value = playerRef;
                        MountPlayer(playerNet.gameObject, true);
                    }
                    else
                    {
                        if (m_NetChildPlayer.Value.TryGet(out _)) return;
                        if (m_NetChildPlayer.CanClientWrite(NetworkManager.LocalClientId)) m_NetChildPlayer.Value = playerRef;
                        MountPlayer(playerNet.gameObject, false);
                    }

                    Debug.Log($"[CoopWoodenBoat] 🌐 Server đã chấp nhận {playerNet.name} lên ghế {(asDad ? "Ba" : "Con")}!");
                }
                else
                {
                    Debug.LogWarning($"[CoopWoodenBoat] 🌐 Server từ chối: {playerNet.name} quá xa thuyền ({dist:F1}m)!");
                }
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestDismountServerRpc(bool isDad, RpcParams rpc = default)
        {
            if (!OwnsSeat(rpc.Receive.SenderClientId, isDad)) return;
            if (isDad)
            {
                if (m_NetDadPlayer.CanClientWrite(NetworkManager.LocalClientId)) m_NetDadPlayer.Value = default;
                DismountPlayer(true);
            }
            else
            {
                if (m_NetChildPlayer.CanClientWrite(NetworkManager.LocalClientId)) m_NetChildPlayer.Value = default;
                DismountPlayer(false);
            }

            Debug.Log($"[CoopWoodenBoat] 🌐 Server đã thực hiện xuống thuyền cho {(isDad ? "Ba" : "Con")}.");
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SendRowStrokeServerRpc(RpcParams rpc = default)
        {
            if (!CanRow(rpc.Receive.SenderClientId) || Time.time - m_LastStrokeAt < 0.08f) return;
            TriggerRowStroke();
        }

        [Rpc(SendTo.Everyone)]
        private void NotifyRowStrokeClientRpc()
        {
            m_IsStrokeActive = true;
            m_StrokePhase = 0f;
            m_IsDadRowing = true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SendRowInputServerRpc(bool isRowing, bool isReverse, RpcParams rpc = default)
        {
            if (!CanRow(rpc.Receive.SenderClientId) || Time.time - m_LastStrokeAt < 0.08f) return;
            if (isRowing) TriggerRowStroke();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SendSteerInputServerRpc(float steerInput, RpcParams rpc = default)
        {
            if (!OwnsSeat(rpc.Receive.SenderClientId, false) && !(soloTestMode && !IsChildMounted && OwnsSeat(rpc.Receive.SenderClientId, true))) return;
            m_ChildSteerInput = Mathf.Clamp(steerInput, -1f, 1f);
        }

        private bool OwnsSeat(ulong sender, bool dad)
        {
            var actor = dad ? m_DadPlayer : m_ChildPlayer;
            return actor != null && actor.TryGetComponent<NetworkObject>(out var net) && net.OwnerClientId == sender;
        }
        private bool CanRow(ulong sender) => OwnsSeat(sender, true) || (soloTestMode && !IsDadMounted && OwnsSeat(sender, false));

        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, boardInteractionRadius);

            Vector3 fwd = GetBoatWorldForward();
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, fwd * 3.5f);

            Vector3 right = GetBoatWorldRight();
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, right * 2.0f);
        }
#endif
    }
}
