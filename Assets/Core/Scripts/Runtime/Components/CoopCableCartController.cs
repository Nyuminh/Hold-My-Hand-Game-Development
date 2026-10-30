using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Co-op "Xe Goòng / Cáp Treo kéo tay qua hẻm vực" (Cable Cart) — Stage 1.
    /// Trục quay:  `Stage1_Winch_Ratchet` / `Stage1_Winch_Ratchet_Lever` (quay tròn khi Cha giữ).
    /// Xe treo:   giỏ gỗ trượt tịnh tiến dọc dây cáp từ bờ A sang bờ B.
    /// Gắn script lên GameObject điều khiển tại bờ A, kéo đủ reference vào Inspector.
    ///
    /// Luồng chơi:
    /// 1. Cha đứng gần tời + GIỮ phím quay ([E] giữ / Gamepad RT) →
    ///    cần tời quay tròn, giỏ gỗ trượt A → B qua vực sâu.
    /// 2. Cha THẢ tay trước khi tới đích → giỏ tự trôi ngược từ từ về A.
    /// 3. Giỏ tới bờ B → Con nhảy ra, gạt chốt khóa ([R] tại chốt bờ B) →
    ///    khóa cáp cố định để đón Cha qua (giỏ không trôi nữa, Cha có thể đi bộ qua / kéo về).
    ///
    /// AN TOÀN (Target Verification): Find dùng tên CHÍNH XÁC (==), không substring;
    /// thiếu reference nào → log cảnh báo + script tự vô hiệu hóa phần đó, không crash.
    /// Transform FBX: nhân offset với rotation gốc (initialRot * Euler), KHÔNG ghi đè.
    /// </summary>
    public class CoopCableCartController : MonoBehaviour
    {
        [Header("Target — Trục tời (tên CHÍNH XÁC)")]
        [Tooltip("Tên CHÍNH XÁC trục ròng rọc quay")]
        [SerializeField] private string winchRatchetName = "Stage1_Winch_Ratchet";
        [Tooltip("Tên CHÍNH XÁC cần tời (tay quay)")]
        [SerializeField] private string winchLeverName = "Stage1_Winch_Ratchet_Lever";
        [SerializeField] private Transform winchRatchet;
        [SerializeField] private Transform winchLever;

        [Header("Target — Dây cáp & Giỏ gỗ")]
        [Tooltip("Điểm neo bờ A (xuất phát)")]
        [SerializeField] private Transform anchorA;
        [Tooltip("Điểm neo bờ B (đích)")]
        [SerializeField] private Transform anchorB;
        [Tooltip("Giỏ gỗ / xe treo trượt dọc cáp")]
        [SerializeField] private Transform cart;
        [Tooltip("Chốt khóa cố định tại bờ B (Con gạt)")]
        [SerializeField] private Transform lockLatchB;

        [Header("Vận hành tời")]
        [Tooltip("Người chơi phải đứng trong bán kính này quanh tời mới quay được (mét)")]
        [SerializeField] private float winchInteractRadius = 3.5f;
        [Tooltip("Tốc độ giỏ chạy A → B khi Cha giữ tời (m/s)")]
        [SerializeField] private float pullSpeed = 2.0f;
        [Tooltip("Tốc độ giỏ trôi ngược về A khi Cha thả tay (m/s)")]
        [SerializeField] private float rollbackSpeed = 0.8f;
        [Tooltip("Tốc độ quay trục tời (độ/giây) khi đang kéo")]
        [SerializeField] private float winchSpinSpeed = 240f;
        [Tooltip("Tốc độ quay cần tời (độ/giây)")]
        [SerializeField] private float leverSpinSpeed = 120f;
        [Tooltip("Ngưỡng coi là ĐÃ TỚI bờ B (mét)")]
        [SerializeField] private float arriveThreshold = 0.35f;

        [Header("Hiệu ứng (tùy chọn)")]
        [SerializeField] private AudioSource winchAudio;
        [SerializeField] private ParticleSystem cartDust;

        [Header("Trạng thái (chỉ đọc)")]
        [SerializeField, Range(0f, 1f)] private float cartProgress;
        [SerializeField] private bool isCranking;
        [SerializeField] private bool arrivedAtB;
        [SerializeField] private bool cableLocked;

        private Quaternion m_WinchInitialRot;
        private Quaternion m_LeverInitialRot;
        private bool m_WinchResolved;
        private bool m_LeverResolved;
        private string m_Prompt = "";
        private Transform m_LocalPlayer;
        private bool m_LatchPlayerInside;

        #region Unity Lifecycle

        private void Awake()
        {
            ResolveTargets();
        }

        private void Update()
        {
            m_Prompt = "";
            m_LocalPlayer = FindLocalPlayer();

            // 0. Chưa khóa cáp → logic kéo / trôi
            if (!cableLocked)
            {
                UpdateCranking();
                UpdateCartMotion();
            }
            else
            {
                m_Prompt = "🔒 Cáp đã khóa — Cha có thể qua!";
                StopWinchAudio();
            }

            // 3. Con tới bờ B → gạt chốt khóa
            UpdateLockLatch();
        }

        private void OnDrawGizmos()
        {
            if (anchorA != null && anchorB != null)
            {
                Gizmos.color = cableLocked ? Color.green : Color.yellow;
                Gizmos.DrawLine(anchorA.position, anchorB.position);
                Gizmos.DrawSphere(anchorA.position, 0.25f);
                Gizmos.DrawSphere(anchorB.position, 0.25f);
            }
            if (cart != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(cart.position, Vector3.one * 0.8f);
            }
        }

        #endregion

        #region 1. Cha giữ tời → quay + kéo giỏ

        private void UpdateCranking()
        {
            isCranking = false;
            if (m_LocalPlayer == null || cart == null || anchorA == null || anchorB == null) return;
            if (arrivedAtB) return; // đã tới đích → chờ Con gạt chốt

            // Cha phải đứng gần tời mới quay được
            Transform crankCenter = winchRatchet != null ? winchRatchet : transform;
            float dist = Vector3.Distance(m_LocalPlayer.position, crankCenter.position);
            if (dist > winchInteractRadius)
            {
                m_Prompt = "Đứng gần tời để quay";
                StopWinchAudio();
                return;
            }

            if (IsCrankHeld())
            {
                isCranking = true;
                m_Prompt = "Đang quay tời… (thả tay = giỏ trôi về)";
                SpinWinch(Time.deltaTime);
                StartWinchAudio();
            }
            else
            {
                m_Prompt = "GIỮ [E] để quay tời kéo giỏ qua vực";
                StopWinchAudio();
            }
        }

        private void SpinWinch(float dt)
        {
            // Nhân offset với rotation GỐC (giữ bù trục FBX), không ghi đè
            if (winchRatchet != null)
                winchRatchet.localRotation = winchRatchet.localRotation
                    * Quaternion.Euler(0f, winchSpinSpeed * dt, 0f);
            if (winchLever != null)
                winchLever.localRotation = winchLever.localRotation
                    * Quaternion.Euler(winchSpinSpeed * 0.5f * dt, 0f, 0f);
        }

        #endregion

        #region 2. Giỏ trượt / trôi ngược

        private void UpdateCartMotion()
        {
            if (cart == null || anchorA == null || anchorB == null) return;
            float cableLen = Vector3.Distance(anchorA.position, anchorB.position);
            if (cableLen < 0.01f) return;

            if (isCranking)
            {
                // Kéo A → B
                cartProgress = Mathf.Clamp01(cartProgress + pullSpeed * Time.deltaTime / cableLen);
            }
            else if (!arrivedAtB && cartProgress > 0f)
            {
                // Thả tay trước đích → trôi ngược từ từ về A
                cartProgress = Mathf.Clamp01(cartProgress - rollbackSpeed * Time.deltaTime / cableLen);
            }

            cart.position = Vector3.Lerp(anchorA.position, anchorB.position, cartProgress);
            // Giỏ luôn hướng dọc dây cáp
            Vector3 dir = (anchorB.position - anchorA.position).normalized;
            if (dir.sqrMagnitude > 0.001f)
                cart.rotation = Quaternion.LookRotation(dir);

            if (cartProgress >= 1f - arriveThreshold / Mathf.Max(cableLen, 1f) && !arrivedAtB)
            {
                arrivedAtB = true;
                isCranking = false;
                StopWinchAudio();
                UISoundManager.PlaySuccess();
                Debug.Log("[CableCart] 🎉 Giỏ đã tới bờ B! Con nhảy ra và gạt chốt [R] để khóa cáp.");
            }
            else if (cartProgress < 0.98f)
            {
                arrivedAtB = false;
            }

            if (cartDust != null)
            {
                if (isCranking && !cartDust.isPlaying) cartDust.Play();
                else if (!isCranking && cartDust.isPlaying) cartDust.Stop();
            }
        }

        #endregion

        #region 3. Con gạt chốt khóa tại bờ B

        private void UpdateLockLatch()
        {
            if (cableLocked || !arrivedAtB) return;
            if (m_LocalPlayer == null || lockLatchB == null) return;

            float d = Vector3.Distance(m_LocalPlayer.position, lockLatchB.position);
            if (d <= winchInteractRadius)
            {
                m_Prompt = "Nhấn [R] để gạt chốt KHÓA cáp";
                m_LatchPlayerInside = true;
                if (IsLatchPressed())
                {
                    cableLocked = true;
                    UISoundManager.PlayConfirm();
                    Debug.Log("[CableCart] 🔒 Con đã gạt chốt — cáp cố định, đón Cha qua!");
                }
            }
            else
            {
                m_LatchPlayerInside = false;
            }
        }

        [ContextMenu("Mở khóa cáp (Reset)")]
        public void UnlockCable()
        {
            cableLocked = false;
            arrivedAtB = false;
            cartProgress = 0f;
            Debug.Log("[CableCart] 🔓 Đã mở khóa — giỏ về bờ A.");
        }

        #endregion

        #region Input (New Input System + Legacy fallback)

        private bool IsCrankHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.isPressed) return true;
            if (Gamepad.current != null && Gamepad.current.rightTrigger.isPressed) return true;
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
#endif
            return Input.GetKey(KeyCode.E) || Input.GetMouseButton(0);
        }

        private bool IsLatchPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) return true;
            if (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame) return true;
#endif
            return Input.GetKeyDown(KeyCode.R);
        }

        #endregion

        #region Target Verification (exact name, không substring)

        private void ResolveTargets()
        {
            // Tên CHÍNH XÁC (==), theo rule No Fuzzy
            if (winchRatchet == null && !string.IsNullOrEmpty(winchRatchetName))
            {
                GameObject go = GameObject.Find(winchRatchetName);
                if (go != null)
                {
                    winchRatchet = go.transform;
                    Debug.Log($"[CableCart] ✔ FOUND '{go.name}' tại {go.transform.position}.");
                }
                else Debug.LogWarning($"[CableCart] ⚠️ MISSING '{winchRatchetName}' — hãy kéo Transform vào Inspector.");
            }
            if (winchLever == null && !string.IsNullOrEmpty(winchLeverName))
            {
                GameObject go = GameObject.Find(winchLeverName);
                if (go != null)
                {
                    winchLever = go.transform;
                    Debug.Log($"[CableCart] ✔ FOUND '{go.name}' tại {go.transform.position}.");
                }
                else Debug.LogWarning($"[CableCart] ⚠️ MISSING '{winchLeverName}' — hãy kéo Transform vào Inspector.");
            }

            m_WinchResolved = winchRatchet != null;
            m_LeverResolved = winchLever != null;

            if (m_WinchResolved) m_WinchInitialRot = winchRatchet.localRotation;
            if (m_LeverResolved) m_LeverInitialRot = winchLever.localRotation;

            if (cart == null) Debug.LogWarning("[CableCart] ⚠️ Chưa gán Cart (giỏ gỗ) — kéo vào Inspector.");
            if (anchorA == null || anchorB == null) Debug.LogWarning("[CableCart] ⚠️ Chưa gán đủ AnchorA/AnchorB.");
            if (lockLatchB == null) Debug.LogWarning("[CableCart] ⚠️ Chưa gán LockLatchB (chốt bờ B).");
        }

        private Transform FindLocalPlayer()
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) return p.transform;
            var cam = Camera.main;
            return cam != null ? cam.transform : null;
        }

        private void StartWinchAudio()
        {
            if (winchAudio != null && winchAudio.clip != null && !winchAudio.isPlaying)
                winchAudio.Play();
        }

        private void StopWinchAudio()
        {
            if (winchAudio != null && winchAudio.isPlaying)
                winchAudio.Stop();
        }

        #endregion

        #region UI

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(m_Prompt)) return;

            // Thanh tiến trình giỏ A → B
            float bw = 320f, bh = 10f;
            float bx = (Screen.width - bw) / 2f, by = Screen.height - 170f;
            GUI.Box(new Rect(bx, by, bw, bh), "");
            GUI.color = cableLocked ? Color.green : Color.cyan;
            GUI.DrawTexture(new Rect(bx, by, bw * cartProgress, bh), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = Color.yellow;
            GUI.Box(new Rect((Screen.width - 460f) / 2f, Screen.height - 130f, 460f, 55f), m_Prompt, style);
        }

        #endregion
    }
}
