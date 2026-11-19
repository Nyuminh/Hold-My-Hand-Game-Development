using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Điều khiển Cầu Kéo & Cần Gạt Co-op cho Zone 1.
    /// Vị trí: Đặt tại bờ Stage1_LockPlate_Pad, chỉa sang bờ Lều trại (Spawn point).
    /// Mặc định: Cầu DỰNG ĐỨNG ĐÓNG CỔNG (Upright / Closed).
    /// Khi người chơi gạt 1 cần: Cầu hạ xuống 45 độ (nửa đường).
    /// Khi gạt đủ 2 cần: Cầu hạ phẳng lì tiếp đất để 2 người chơi cùng chạy qua!
    /// Dây xích sắt bám dính chính xác 100% từ ròng rọc trên trụ tới mép cầu ở mọi góc độ.
    /// Hỗ trợ NetworkBehaviour đồng bộ hoàn toàn giữa Host và Client qua NetworkVariable & ServerRpc.
    /// </summary>
    public class CoopDrawbridgeController : NetworkBehaviour
    {
        private static CoopDrawbridgeController instance;
        public static CoopDrawbridgeController Instance => instance;

        public bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;

        private readonly NetworkVariable<bool> netIsLeftPulled = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> netIsRightPulled = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Header("Target References")]
        [Tooltip("Bản lề xoay của cầu (Zone1_Drawbridge_Hinge)")]
        [SerializeField] private Transform bridgeHinge;

        [Tooltip("Cần gạt bên trái (Zone1_Lever_Hinge_Left)")]
        [SerializeField] private Transform leverLeft;

        [Tooltip("Cần gạt bên phải (Zone1_Lever_Hinge_Right)")]
        [SerializeField] private Transform leverRight;

        [Tooltip("Bánh ròng rọc trái trên trụ (Zone1_Pulley_Left)")]
        [SerializeField] private Transform pulleyLeft;

        [Tooltip("Bánh ròng rọc phải trên trụ (Zone1_Pulley_Right)")]
        [SerializeField] private Transform pulleyRight;

        [Tooltip("Vòng khuyên xích trên mép cầu trái (Zone1_Drawbridge_Ring_Left)")]
        [SerializeField] private Transform ringLeft;

        [Tooltip("Vòng khuyên xích trên mép cầu phải (Zone1_Drawbridge_Ring_Right)")]
        [SerializeField] private Transform ringRight;

        [Tooltip("Dây xích trái (Zone1_Drawbridge_Chain_Left)")]
        [SerializeField] private Transform chainLeft;

        [Tooltip("Dây xích phải (Zone1_Drawbridge_Chain_Right)")]
        [SerializeField] private Transform chainRight;

        [Header("Initial State Settings")]
        [Tooltip("Mặc định ban đầu cầu DỰNG ĐỨNG (true). Gạt cần thì cầu mới hạ xuống.")]
        [SerializeField] private bool startUpright = true;

        [Header("Interaction Settings")]
        [Tooltip("Khoảng cách tương tác từ nhân vật tới cần gạt (mét)")]
        [SerializeField] private float interactDistance = 3.5f;

        [Header("Bridge Angles (Offset from base upright pose)")]
        [Tooltip("Góc khi cầu DỰNG ĐỨNG (0 độ lệch so với model gốc)")]
        [SerializeField] private float uprightAngleOffset = 0f;

        [Tooltip("Góc khi gạt 1 cần (hạ xuống nghiêng ~40-45 độ)")]
        [SerializeField] private float halfLowerAngleOffset = 38f;

        [Tooltip("Góc khi gạt đủ 2 cần (hạ phẳng lì tiếp đất ~78 độ)")]
        [SerializeField] private float fullLowerAngleOffset = 78f;

        [Tooltip("Thời gian chuyển động nâng/hạ cầu (giây)")]
        [SerializeField] private float transitionDuration = 2.0f;

        [Header("Current Status")]
        [SerializeField] private bool isLeftPulled = false;
        [SerializeField] private bool isRightPulled = false;
        [SerializeField] private float currentBridgeAngle = 0f;

        /// <summary>Cha đã gạt cần trái (cầu hạ 45°).</summary>
        public bool IsLeftPulled => isLeftPulled;
        /// <summary>Con đã gạt cần phải (cầu hạ phẳng 0°).</summary>
        public bool IsRightPulled => isRightPulled;
        /// <summary>Cầu đã hạ phẳng tiếp đất (cả 2 cần đã gạt).</summary>
        public bool IsFullyLowered => isLeftPulled && isRightPulled;
        /// <summary>Góc cầu hiện tại (độ).</summary>
        public float CurrentBridgeAngle => currentBridgeAngle;

        private Quaternion initialHingeRot;
        private Quaternion initialLeverLeftRot;
        private Quaternion initialLeverRightRot;

        private float initialChainDistLeft = 0f;
        private float initialChainDistRight = 0f;
        private Coroutine bridgeCoroutine;

        private Camera mainCam;
        private string currentPrompt = "";

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;

            mainCam = Camera.main;
            AutoFindReferences();

            if (bridgeHinge != null)
                initialHingeRot = bridgeHinge.localRotation;
            if (leverLeft != null)
                initialLeverLeftRot = leverLeft.localRotation;
            if (leverRight != null)
                initialLeverRightRot = leverRight.localRotation;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            netIsLeftPulled.OnValueChanged += OnNetworkLeftPulledChanged;
            netIsRightPulled.OnValueChanged += OnNetworkRightPulledChanged;

            if (netIsLeftPulled.Value != isLeftPulled)
                ApplyLeverVisual(true, netIsLeftPulled.Value, false);
            if (netIsRightPulled.Value != isRightPulled)
                ApplyLeverVisual(false, netIsRightPulled.Value, false);
        }

        public override void OnNetworkDespawn()
        {
            netIsLeftPulled.OnValueChanged -= OnNetworkLeftPulledChanged;
            netIsRightPulled.OnValueChanged -= OnNetworkRightPulledChanged;
            base.OnNetworkDespawn();
        }

        private void OnNetworkLeftPulledChanged(bool previousValue, bool newValue)
        {
            ApplyLeverVisual(true, newValue, true);
        }

        private void OnNetworkRightPulledChanged(bool previousValue, bool newValue)
        {
            ApplyLeverVisual(false, newValue, true);
        }

        private void Start()
        {
            // Ghi nhận khoảng cách xích ban đầu ở trạng thái model để scale chuẩn 1:1
            if (pulleyLeft != null && ringLeft != null)
                initialChainDistLeft = Vector3.Distance(pulleyLeft.position, ringLeft.position);
            if (pulleyRight != null && ringRight != null)
                initialChainDistRight = Vector3.Distance(pulleyRight.position, ringRight.position);

            if (bridgeHinge != null)
            {
                if (IsNetworkActive)
                {
                    UpdateBridgeTargetFromLevers();
                }
                else
                {
                    currentBridgeAngle = startUpright ? uprightAngleOffset : fullLowerAngleOffset;
                    bridgeHinge.localRotation = initialHingeRot * Quaternion.Euler(0f, currentBridgeAngle, 0f);
                }
            }

            UpdateChains();
        }

        private void AutoFindReferences()
        {
            if (bridgeHinge == null)
            {
                var h = GameObject.Find("Zone1_Drawbridge_Hinge");
                if (h != null) bridgeHinge = h.transform;
            }

            if (leverLeft == null)
            {
                var l = GameObject.Find("Zone1_Lever_Hinge_Left");
                if (l != null) leverLeft = l.transform;
            }

            if (leverRight == null)
            {
                var r = GameObject.Find("Zone1_Lever_Hinge_Right");
                if (r != null) leverRight = r.transform;
            }

            if (pulleyLeft == null)
            {
                var pl = GameObject.Find("Zone1_Pulley_Left");
                if (pl != null) pulleyLeft = pl.transform;
            }

            if (pulleyRight == null)
            {
                var pr = GameObject.Find("Zone1_Pulley_Right");
                if (pr != null) pulleyRight = pr.transform;
            }

            if (ringLeft == null)
            {
                var rl = GameObject.Find("Zone1_Drawbridge_Ring_Left");
                if (rl != null) ringLeft = rl.transform;
            }

            if (ringRight == null)
            {
                var rr = GameObject.Find("Zone1_Drawbridge_Ring_Right");
                if (rr != null) ringRight = rr.transform;
            }

            if (chainLeft == null)
            {
                var cl = GameObject.Find("Zone1_Drawbridge_Chain_Left");
                if (cl != null) chainLeft = cl.transform;
            }

            if (chainRight == null)
            {
                var cr = GameObject.Find("Zone1_Drawbridge_Chain_Right");
                if (cr != null) chainRight = cr.transform;
            }
        }

        private void Update()
        {
            currentPrompt = "";

            Transform player = FindLocalPlayer();
            if (player == null) return;

            bool interactPressed = IsInteractPressed();

            float distLeft = leverLeft != null ? Vector3.Distance(player.position, leverLeft.position) : float.MaxValue;
            float distRight = leverRight != null ? Vector3.Distance(player.position, leverRight.position) : float.MaxValue;

            // Ưu tiên cần gạt gần nhất nếu cả hai đều nằm trong tầm
            if (distLeft <= interactDistance && distLeft <= distRight)
            {
                bool isDad = IsPlayerDad(player);
                if (!isDad)
                {
                    currentPrompt = "<color=#FFAA00>⚠️ [Con] Cần gạt bên trái quá nặng! Chỉ có 🧔 Ba mới đủ sức gạt!</color>";
                }
                else
                {
                    currentPrompt = isLeftPulled ? "👨 [Ba] Nhấn [R] để nhả Cần Bên Trái" : "👨 [Ba] Nhấn [R] để gạt Cần Bên Trái";
                    if (interactPressed)
                    {
                        ToggleLever(true);
                    }
                }
            }
            else if (distRight <= interactDistance)
            {
                bool isDad = IsPlayerDad(player);
                currentPrompt = isRightPulled ? (isDad ? "👨 [Ba] Nhấn [R] để nhả Cần Bên Phải" : "👶 [Con] Nhấn [R] để nhả Cần Bên Phải") : (isDad ? "👨 [Ba] Nhấn [R] để gạt Cần Bên Phải" : "👶 [Con] Nhấn [R] để gạt Cần Bên Phải");
                if (interactPressed)
                {
                    ToggleLever(false);
                }
            }
        }

        private void LateUpdate()
        {
            UpdateChains();
        }

        private bool IsInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                return true;
            }

            if (Gamepad.current != null && (Gamepad.current.buttonWest.wasPressedThisFrame || Gamepad.current.buttonNorth.wasPressedThisFrame))
            {
                return true;
            }
            return false;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }

        public void ToggleLever(bool isLeft)
        {
            if (IsNetworkActive)
            {
                if (IsServer)
                {
                    if (isLeft) netIsLeftPulled.Value = !netIsLeftPulled.Value;
                    else netIsRightPulled.Value = !netIsRightPulled.Value;
                }
                else
                {
                    ToggleLeverServerRpc(isLeft);
                }
                return;
            }

            // Fallback khi chạy solo/offline trong Editor hoặc chưa spawn qua mạng
            bool newPulled = isLeft ? !isLeftPulled : !isRightPulled;
            ApplyLeverVisual(isLeft, newPulled, true);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ToggleLeverServerRpc(bool isLeft)
        {
            if (isLeft) netIsLeftPulled.Value = !netIsLeftPulled.Value;
            else netIsRightPulled.Value = !netIsRightPulled.Value;
        }

        private void ApplyLeverVisual(bool isLeft, bool pulled, bool animate)
        {
            if (isLeft)
            {
                isLeftPulled = pulled;
                if (leverLeft != null)
                {
                    if (animate && gameObject.activeInHierarchy) StartCoroutine(AnimateLeverHandle(leverLeft, initialLeverLeftRot, isLeftPulled));
                    else leverLeft.localRotation = initialLeverLeftRot * Quaternion.Euler(0f, isLeftPulled ? 35f : 0f, 0f);
                }
            }
            else
            {
                isRightPulled = pulled;
                if (leverRight != null)
                {
                    if (animate && gameObject.activeInHierarchy) StartCoroutine(AnimateLeverHandle(leverRight, initialLeverRightRot, isRightPulled));
                    else leverRight.localRotation = initialLeverRightRot * Quaternion.Euler(0f, isRightPulled ? 35f : 0f, 0f);
                }
            }

            UpdateBridgeTargetFromLevers();
        }

        private void UpdateBridgeTargetFromLevers()
        {
            int pulledCount = (isLeftPulled ? 1 : 0) + (isRightPulled ? 1 : 0);
            float targetAngle;

            if (startUpright)
            {
                if (pulledCount == 0) targetAngle = uprightAngleOffset;
                else if (pulledCount == 1) targetAngle = halfLowerAngleOffset;
                else targetAngle = fullLowerAngleOffset;
            }
            else
            {
                if (pulledCount == 0) targetAngle = fullLowerAngleOffset;
                else if (pulledCount == 1) targetAngle = halfLowerAngleOffset;
                else targetAngle = uprightAngleOffset;
            }

            SetBridgeAngle(targetAngle);
        }

        [ContextMenu("Lower Bridge (Hạ Cầu Phẳng Lì)")]
        public void LowerBridge()
        {
            SetBridgeAngle(fullLowerAngleOffset);
        }

        [ContextMenu("Half Lower Bridge (Hạ Cầu Nghiêng 45 độ)")]
        public void HalfLowerBridge()
        {
            SetBridgeAngle(halfLowerAngleOffset);
        }

        [ContextMenu("Raise Bridge (Dựng Đứng Đóng Cửa)")]
        public void RaiseBridge()
        {
            SetBridgeAngle(uprightAngleOffset);
        }

        private void SetBridgeAngle(float targetAngle)
        {
            if (bridgeHinge == null) return;
            if (bridgeCoroutine != null) StopCoroutine(bridgeCoroutine);
            bridgeCoroutine = StartCoroutine(AnimateBridgeRoutine(targetAngle));
        }

        private IEnumerator AnimateBridgeRoutine(float targetAngle)
        {
            float startAngle = currentBridgeAngle;
            float elapsed = 0f;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);
                currentBridgeAngle = Mathf.Lerp(startAngle, targetAngle, t);
                bridgeHinge.localRotation = initialHingeRot * Quaternion.Euler(0f, currentBridgeAngle, 0f);

                // Hiệu ứng quay ròng rọc khi nâng/hạ
                float spin = Time.deltaTime * 240f * (targetAngle > startAngle ? 1f : -1f);
                if (pulleyLeft != null) pulleyLeft.Rotate(Vector3.up, spin, Space.Self);
                if (pulleyRight != null) pulleyRight.Rotate(Vector3.up, spin, Space.Self);

                yield return null;
            }

            currentBridgeAngle = targetAngle;
            bridgeHinge.localRotation = initialHingeRot * Quaternion.Euler(0f, currentBridgeAngle, 0f);
            bridgeCoroutine = null;
        }

        private IEnumerator AnimateLeverHandle(Transform leverHingeTransform, Quaternion initialRot, bool pulled)
        {
            Quaternion startRot = leverHingeTransform.localRotation;
            Quaternion targetRot = initialRot * Quaternion.Euler(0f, pulled ? 35f : 0f, 0f);
            float elapsed = 0f;
            float dur = 0.35f;

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                leverHingeTransform.localRotation = Quaternion.Slerp(startRot, targetRot, elapsed / dur);
                yield return null;
            }
            leverHingeTransform.localRotation = targetRot;
        }

        private void UpdateChains()
        {
            UpdateSingleChain(chainLeft, pulleyLeft, ringLeft, initialChainDistLeft);
            UpdateSingleChain(chainRight, pulleyRight, ringRight, initialChainDistRight);
        }

        private void UpdateSingleChain(Transform chain, Transform pulley, Transform ring, float initialDist)
        {
            if (chain == null || pulley == null || ring == null) return;

            Vector3 startP = pulley.position;
            Vector3 endP = ring.position;

            chain.position = (startP + endP) * 0.5f;

            Vector3 dir = endP - startP;
            if (dir.sqrMagnitude > 0.001f)
            {
                chain.rotation = Quaternion.LookRotation(dir);
            }

            float currentDist = dir.magnitude;
            if (initialDist > 0.1f)
            {
                float scaleZ = currentDist / initialDist;
                chain.localScale = new Vector3(chain.localScale.x, chain.localScale.y, scaleZ);
            }
        }

        private Transform FindLocalPlayer()
        {
            // Ưu tiên tìm nhân vật của máy local (IsOwner)
            var players = GameObject.FindGameObjectsWithTag("Player");
            if (players != null && players.Length > 0)
            {
                foreach (var p in players)
                {
                    if (p == null) continue;
                    var no = p.GetComponentInParent<NetworkObject>();
                    if (no != null && no.IsOwner)
                    {
                        return p.transform;
                    }
                }
                // Fallback khi chạy solo/offline trong Editor
                return players[0].transform;
            }

            if (mainCam != null) return mainCam.transform;

            return null;
        }

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(currentPrompt))
            {
                GUIStyle style = new GUIStyle(GUI.skin.box);
                style.fontSize = 20;
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = Color.yellow;
                style.alignment = TextAnchor.MiddleCenter;

                float width = 420f;
                float height = 55f;
                float x = (Screen.width - width) / 2f;
                float y = Screen.height - 130f;

                GUI.Box(new Rect(x, y, width, height), currentPrompt, style);
            }
        }

        public static bool IsPlayerDad(Transform playerTransform)
        {
            if (playerTransform == null) return false;
            var visuals = playerTransform.GetComponentInChildren<PlayerRoleVisuals>();
            if (visuals != null) return visuals.IsDad;
            var boost = playerTransform.GetComponentInChildren<CoopBoostThrowAbility>();
            if (boost != null) return boost.IsDad;
            string n = playerTransform.gameObject.name.ToLower();
            if (n.Contains("dad") || n.Contains("father")) return true;
            if (n.Contains("child") || n.Contains("son")) return false;
            return true;
        }
}
}
