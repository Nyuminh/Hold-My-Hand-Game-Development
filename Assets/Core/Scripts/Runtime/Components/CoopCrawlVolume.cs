using System.Collections.Generic;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Vùng Hạn Chế Khe Hẹp (Asymmetric Crawl Volume):
    /// - Chỉ cho phép 👶 Con (Child) với vóc dáng nhỏ nhắn chui lọt qua.
    /// - Khi Con vào bên trong: Tự động hạ chiều cao CharacterController để khom người bò qua.
    /// - Khi 🧔 Ba (Dad) cố tình chui vào: Bị chặn lại, đẩy lùi và hiển thị cảnh báo yêu cầu Con vào trợ giúp.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CoopCrawlVolume : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Cấu hình Chiều cao Chui (Crawl Height)")]
        [Tooltip("Chiều cao của Con khi chui qua khe này (m)")]
        [SerializeField] private float crawlHeight = 0.9f;

        [Tooltip("Lực đẩy lùi Ba khi cố tình đi vào (Impulse)")]
        [SerializeField] private float dadPushbackForce = 4.5f;

        [Header("Thông điệp cảnh báo")]
        [Tooltip("Nội dung thông báo khi Ba bị chặn")]
        [SerializeField] private string dadBlockedMessage = "🚫 🧔 Ba quá to lớn, không thể chui qua khe này! Hãy để 👶 Con chui vào mở đường!";

        #endregion

        #region Private Fields

        private Collider m_Collider;
        private readonly Dictionary<CharacterController, float> m_OriginalHeights = new Dictionary<CharacterController, float>();
        private readonly Dictionary<CharacterController, Vector3> m_OriginalCenters = new Dictionary<CharacterController, Vector3>();
        private string m_CurrentWarning = "";
        private float m_WarningTimer = 0f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            m_Collider = GetComponent<Collider>();
            m_Collider.isTrigger = true;
        }

        private void Update()
        {
            if (m_WarningTimer > 0f)
            {
                m_WarningTimer -= Time.deltaTime;
                if (m_WarningTimer <= 0f)
                {
                    m_CurrentWarning = "";
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleCharacterEnter(other);
        }

        private void OnTriggerStay(Collider other)
        {
            // Đảm bảo Ba không thể lách sâu vào bên trong
            var visuals = other.GetComponentInParent<PlayerRoleVisuals>();
            if (visuals != null && visuals.IsDad)
            {
                var motor = other.GetComponentInParent<CoreMovement>();
                if (motor != null)
                {
                    Vector3 pushDir = (motor.transform.position - transform.position);
                    pushDir.y = 0f;
                    if (pushDir.sqrMagnitude < 0.01f) pushDir = -motor.transform.forward;
                    motor.ApplyExternalForce(pushDir.normalized * (dadPushbackForce * 0.5f), ForceMode.Impulse);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            HandleCharacterExit(other);
        }

        #endregion

        #region Trigger Handlers

        private void HandleCharacterEnter(Collider other)
        {
            var visuals = other.GetComponentInParent<PlayerRoleVisuals>();
            var motor = other.GetComponentInParent<CoreMovement>();
            var controller = other.GetComponentInParent<CharacterController>();

            if (controller == null) return;

            bool isDad = visuals != null ? visuals.IsDad : (other.name.ToLower().Contains("dad") || other.transform.root.name.ToLower().Contains("dad"));

            if (isDad)
            {
                // Ba bị chặn đứng
                if (motor != null)
                {
                    Vector3 pushDir = -motor.transform.forward;
                    motor.ApplyExternalForce(pushDir * dadPushbackForce, ForceMode.Impulse);
                }

                m_CurrentWarning = dadBlockedMessage;
                m_WarningTimer = 3.0f;
                UISoundManager.PlayClick();
                Debug.Log($"[CoopCrawlVolume] 🚫 🧔 Ba đã bị chặn bởi khe hẹp '{gameObject.name}'!");
            }
            else
            {
                // Con được phép chui qua -> Hạ chiều cao CharacterController
                if (!m_OriginalHeights.ContainsKey(controller))
                {
                    m_OriginalHeights[controller] = controller.height;
                    m_OriginalCenters[controller] = controller.center;
                }

                controller.height = crawlHeight;
                controller.center = new Vector3(
                    controller.center.x, 
                    crawlHeight / 2f, 
                    controller.center.z
                );

                Debug.Log($"[CoopCrawlVolume] 👶 Con đang khom người chui qua khe hẹp '{gameObject.name}'!");
            }
        }

        private void HandleCharacterExit(Collider other)
        {
            var controller = other.GetComponentInParent<CharacterController>();
            if (controller == null) return;

            // Khôi phục chiều cao ban đầu khi ra khỏi khe hẹp
            if (m_OriginalHeights.TryGetValue(controller, out float originalHeight))
            {
                controller.height = originalHeight;
                if (m_OriginalCenters.TryGetValue(controller, out Vector3 originalCenter))
                {
                    controller.center = originalCenter;
                }

                m_OriginalHeights.Remove(controller);
                m_OriginalCenters.Remove(controller);
                Debug.Log($"[CoopCrawlVolume] 👶 Con đã chui ra ngoài an toàn và đứng thẳng dậy.");
            }
        }

        #endregion

        #region Debug UI & Gizmos

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(m_CurrentWarning))
            {
                GUI.color = new Color(1f, 0.35f, 0.35f);
                GUI.Box(new Rect(Screen.width / 2f - 260, 80, 520, 45), 
                    $"\n{m_CurrentWarning}");
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }

        #endregion
    }
}
