using UnityEngine;
using Unity.Netcode;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Co-op "Cha Đỡ Con Nhảy Lên Cao" (Boost Pad) — Zone 1, vách đá cạnh cổng thành.
    /// Vị trí áp dụng: mỏm đá thấp gần `Cliff_L_Spawn` và `Zone1_Gate_Pillar_Left`.
    /// Gắn script này lên một GameObject có BoxCollider (isTrigger = true) đặt tại mỏm đá thấp.
    ///
    /// Luồng chơi:
    /// 1. Cha vào trigger → hiện prompt "Nhấn [F] để làm điểm tựa nâng Con".
    ///    Cha nhấn [F] → vào tư thế gồng đỡ (crouch giữ).
    /// 2. Con bước vào trong lúc Cha đang đỡ → Con nhấn [Space] →
    ///    Con nhận lực nhảy Vector3.up * boostUpVelocity kèm đẩy ngang về mép đá cao.
    /// 3. Phát bụi chân (Particle) + âm thanh khi bật nhảy.
    ///
    /// AN TOÀN (Target Verification): không đoán mò — mọi GetComponent đều kiểm tra null,
    /// mọi Find đều dùng tên CHÍNH XÁC (GameObject.Find exact name), thiếu thì log cảnh báo.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class CoopBoostPad : MonoBehaviour
    {
        [Header("Neo vị trí (chỉ để kiểm chứng, không bắt buộc)")]
        [Tooltip("Tên CHÍNH XÁC mỏm đá thấp (kiểm chứng vị trí đặt pad)")]
        [SerializeField] private string lowLedgeAnchorName = "Cliff_L_Spawn";
        [Tooltip("Tên CHÍNH XÁC trụ cổng trái (kiểm chứng vị trí đặt pad)")]
        [SerializeField] private string gatePillarName = "Zone1_Gate_Pillar_Left";

        [Header("Đích bật nhảy")]
        [Tooltip("Neo mép đá CAO (kéo vào đây). Nếu trống, dùng hướng forward của pad")]
        [SerializeField] private Transform highLedgeAnchor;
        [Tooltip("Lực nhảy thẳng đứng cho Con (m/s). Thiết kế: 8.5")]
        [SerializeField] private float boostUpVelocity = 8.5f;
        [Tooltip("Lực đẩy ngang về phía mép đá cao (m/s)")]
        [SerializeField] private float forwardPushSpeed = 3.0f;
        [Tooltip("Hồi chiêu giữa 2 lần đỡ (giây)")]
        [SerializeField] private float cooldown = 1.0f;

        [Header("Hiệu ứng")]
        [Tooltip("Particle bụi chân khi bật nhảy (tùy chọn)")]
        [SerializeField] private ParticleSystem dustParticles;
        [Tooltip("AudioSource phát khi bật nhảy (tùy chọn, fallback UISoundManager)")]
        [SerializeField] private AudioSource jumpAudio;

        [Header("Trạng thái (chỉ đọc)")]
        [SerializeField] private bool dadInside;
        [SerializeField] private bool childInside;
        [SerializeField] private bool dadBracing;

        private CoreMovement m_DadMotor;
        private CoreMovement m_ChildMotor;
        private GameObject m_DadObj;
        private GameObject m_ChildObj;
        private float m_CooldownTimer;
        private string m_Prompt = "";
        private BoxCollider m_Trigger;

        #region Unity Lifecycle

        private void Reset()
        {
            EnsureTrigger();
        }

        private void Awake()
        {
            EnsureTrigger();
            VerifyAnchorTargets();
        }

        private void Update()
        {
            if (m_CooldownTimer > 0f) m_CooldownTimer -= Time.deltaTime;
            m_Prompt = "";

            // Cha vào vùng → prompt + nhấn [F] để đỡ
            if (dadInside && m_DadMotor != null)
            {
                if (IsLocalCharacter(m_DadObj))
                {
                    if (!dadBracing)
                    {
                        m_Prompt = "Nhấn [F] để làm điểm tựa nâng Con";
                        if (IsBracePressed())
                        {
                            dadBracing = true;
                            m_DadMotor.SetCrouchState(true); // tư thế gồng đỡ
                            UISoundManager.PlayClick();
                            Debug.Log("[CoopBoostPad] 🧔 Cha vào tư thế điểm tựa (giữ [F] để duy trì).");
                        }
                    }
                    else
                    {
                        m_Prompt = "Đang đỡ Con… (nhấn [F] để hủy)";
                        if (IsBracePressed())
                        {
                            CancelBrace();
                        }
                    }
                }
                else if (dadBracing)
                {
                    m_Prompt = "Cha đang đỡ — Con vào vị trí và nhấn [Space]";
                }
            }

            // Con vào vùng trong lúc Cha đỡ → nhấn [Space] để bật nhảy
            if (dadBracing && childInside && m_ChildMotor != null)
            {
                if (IsLocalCharacter(m_ChildObj))
                {
                    m_Prompt = "Nhấn [Space] để bật lên mép đá cao!";
                    if (IsJumpPressed() && m_CooldownTimer <= 0f)
                    {
                        ExecuteBoost();
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            CoreMovement motor = other.GetComponent<CoreMovement>();
            if (motor == null) motor = other.GetComponentInParent<CoreMovement>();
            if (motor == null) return; // KHÔNG phải nhân vật → bỏ qua (verify trước khi gán)

            if (IsDadRole(motor.gameObject))
            {
                m_DadMotor = motor;
                m_DadObj = motor.gameObject;
                dadInside = true;
                Debug.Log($"[CoopBoostPad] 🧔 Cha '{m_DadObj.name}' đã vào vùng đỡ tại {transform.position}.");
            }
            else
            {
                m_ChildMotor = motor;
                m_ChildObj = motor.gameObject;
                childInside = true;
                Debug.Log($"[CoopBoostPad] 👶 Con '{m_ChildObj.name}' đã vào vùng đỡ tại {transform.position}.");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            CoreMovement motor = other.GetComponent<CoreMovement>();
            if (motor == null) motor = other.GetComponentInParent<CoreMovement>();
            if (motor == null) return;

            if (motor == m_DadMotor)
            {
                Debug.Log($"[CoopBoostPad] 🧔 Cha '{motor.name}' rời vùng đỡ.");
                m_DadMotor = null;
                m_DadObj = null;
                dadInside = false;
                CancelBrace();
            }
            else if (motor == m_ChildMotor)
            {
                Debug.Log($"[CoopBoostPad] 👶 Con '{motor.name}' rời vùng đỡ.");
                m_ChildMotor = null;
                m_ChildObj = null;
                childInside = false;
            }
        }

        private void OnDisable()
        {
            CancelBrace();
        }

        #endregion

        #region Boost Logic

        private void ExecuteBoost()
        {
            if (m_ChildMotor == null) return;

            // Hướng đẩy ngang: về phía mép đá cao (ưu tiên anchor), fallback forward của pad
            Vector3 pushDir;
            if (highLedgeAnchor != null)
            {
                pushDir = highLedgeAnchor.position - m_ChildMotor.transform.position;
                pushDir.y = 0f;
                pushDir = pushDir.sqrMagnitude > 0.01f ? pushDir.normalized
                    : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            }
            else
            {
                pushDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                if (pushDir.sqrMagnitude < 0.01f) pushDir = m_ChildMotor.transform.forward;
                Debug.LogWarning("[CoopBoostPad] ⚠️ Chưa gán High Ledge Anchor — dùng hướng forward của pad.");
            }

            // Quay mặt Con về hướng mép đá trước khi bật
            if (pushDir.sqrMagnitude > 0.01f)
                m_ChildMotor.transform.rotation = Quaternion.LookRotation(pushDir);

            // Lực nhảy: up * 8.5 + đẩy ngang (verify motor trước khi gán lực)
            m_ChildMotor.SetVerticalVelocity(boostUpVelocity);
            m_ChildMotor.ApplyExternalForce(pushDir * forwardPushSpeed, ForceMode.Impulse);
            m_CooldownTimer = cooldown;

            // Hiệu ứng bụi chân + âm thanh
            if (dustParticles != null) dustParticles.Play();
            if (jumpAudio != null && jumpAudio.clip != null) jumpAudio.Play();
            else UISoundManager.PlayConfirm();

            Debug.Log($"[CoopBoostPad] 🚀 Con bật nhảy! up={boostUpVelocity} + push {pushDir}*{forwardPushSpeed}");
        }

        private void CancelBrace()
        {
            if (!dadBracing) return;
            dadBracing = false;
            if (m_DadMotor != null) m_DadMotor.SetCrouchState(false);
        }

        #endregion

        #region Input (New Input System + Legacy fallback)

        private bool IsBracePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) return true;
            if (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame) return true;
#endif
            return Input.GetKeyDown(KeyCode.F);
        }

        private bool IsJumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) return true;
            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
#endif
            return Input.GetKeyDown(KeyCode.Space);
        }

        #endregion

        #region Role & Target Verification

        /// <summary>
        /// Xác định Cha/Con: ưu tiên PlayerRoleVisuals → CoopBoostThrowAbility → chiều cao → tên.
        /// (Không dùng fuzzy delete; chỉ đọc role, không xóa vật thể.)
        /// </summary>
        private bool IsDadRole(GameObject go)
        {
            if (go == null) return false;
            var visuals = go.GetComponent<PlayerRoleVisuals>();
            if (visuals != null) return visuals.IsDad;
            var boost = go.GetComponent<CoopBoostThrowAbility>();
            if (boost != null) return boost.IsDad;
            var cc = go.GetComponent<CharacterController>();
            if (cc != null && cc.height < 1.45f) return false; // Con cao 1.1m
            string n = go.name.ToLower();
            if (n.Contains("child") || n.Contains("son")) return false;
            if (n.Contains("dad") || n.Contains("father")) return true;
            return true; // mặc định coi là Cha để Con luôn được ưu tiên bật nhảy
        }

        /// <summary>Chỉ nhân vật do máy này điều khiển mới đọc phím (tránh double-trigger mạng).</summary>
        private bool IsLocalCharacter(GameObject go)
        {
            if (go == null) return false;
            if (go.GetComponent<SoloCompanionDummy>() != null) return false;
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null)
                return true; // Tutorial / offline → tất cả là local
            var local = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (local != null) return local.gameObject == go;
            var net = go.GetComponent<NetworkObject>();
            return net != null && net.IsOwner;
        }

        /// <summary>Kiểm chứng vật thể mục tiêu TRƯỚC khi dùng (in Exact Name + Position).</summary>
        private void VerifyAnchorTargets()
        {
            foreach (string exactName in new[] { lowLedgeAnchorName, gatePillarName })
            {
                if (string.IsNullOrEmpty(exactName)) continue;
                GameObject go = GameObject.Find(exactName); // exact match, KHÔNG substring
                if (go != null)
                    Debug.Log($"[CoopBoostPad] ✔ FOUND '{go.name}' tại {go.transform.position} (parent: {(go.transform.parent != null ? go.transform.parent.name : "root")}).");
                else
                    Debug.LogWarning($"[CoopBoostPad] ⚠️ MISSING '{exactName}' — pad vẫn chạy độc lập, hãy kéo High Ledge Anchor thủ công.");
            }
        }

        private void EnsureTrigger()
        {
            m_Trigger = GetComponent<BoxCollider>();
            if (m_Trigger != null && !m_Trigger.isTrigger)
            {
                m_Trigger.isTrigger = true;
                Debug.Log("[CoopBoostPad] Đã bật isTrigger cho BoxCollider.");
            }
        }

        #endregion

        #region UI & Gizmos

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(m_Prompt)) return;
            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = Color.yellow;
            float w = 460f, h = 55f;
            GUI.Box(new Rect((Screen.width - w) / 2f, Screen.height - 130f, w, h), m_Prompt, style);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = dadBracing ? Color.green : new Color(1f, 0.8f, 0.1f, 0.9f);
            var col = GetComponent<BoxCollider>();
            if (col != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(col.center, col.size);
            }
            else
            {
                Gizmos.DrawWireCube(transform.position + Vector3.up * 1f, new Vector3(3f, 2f, 3f));
            }
            // Mũi tên chỉ hướng bật về mép đá cao
            Vector3 target = highLedgeAnchor != null ? highLedgeAnchor.position
                : transform.position + transform.forward * 3f + Vector3.up * 2f;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position + Vector3.up * 1f, target);
            Gizmos.DrawSphere(target, 0.25f);
        }

        #endregion
    }
}
