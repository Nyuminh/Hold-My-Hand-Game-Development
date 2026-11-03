using System.Collections.Generic;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Vùng Hạn Chế Khe Hẹp (Asymmetric Crawl Volume):
    /// - Chỉ cho phép 👶 Con (Child) với vóc dáng nhỏ nhắn chui lọt qua.
    /// - Khi Con vào bên trong: Tự động hạ chiều cao CharacterController và ép dẹp scale hình ảnh (Crawl Pose) để khom người bò qua.
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
        private readonly Dictionary<Transform, Vector3> m_OriginalModelScales = new Dictionary<Transform, Vector3>();
        private readonly List<Transform> m_ActiveCrawlers = new List<Transform>();

        private string m_CurrentWarning = "";
        private float m_WarningTimer = 0f;
        private bool m_ChildIsCrawling = false;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            m_Collider = GetComponent<Collider>();
            m_Collider.isTrigger = true;
        }

        private void Update()
        {
            // Death/teleport can disable the controller without sending OnTriggerExit.
            var departed = new List<CharacterController>();
            foreach (var entry in m_OriginalHeights)
            {
                if (entry.Key == null) continue;
                if (!entry.Key.enabled || !m_Collider.bounds.Intersects(entry.Key.bounds)) departed.Add(entry.Key);
            }
            foreach (var controller in departed) HandleCharacterExit(controller);
            if (m_WarningTimer > 0f)
            {
                m_WarningTimer -= Time.deltaTime;
                if (m_WarningTimer <= 0f)
                {
                    m_CurrentWarning = "";
                }
            }

            // Mượt mà điều chỉnh tỷ lệ cơ thể khi bò qua khe
            for (int i = m_ActiveCrawlers.Count - 1; i >= 0; i--)
            {
                var crawler = m_ActiveCrawlers[i];
                if (crawler == null)
                {
                    m_ActiveCrawlers.RemoveAt(i);
                    continue;
                }

                if (m_OriginalModelScales.TryGetValue(crawler, out Vector3 origScale))
                {
                    Vector3 targetScale = new Vector3(origScale.x * 1.05f, origScale.y * 0.58f, origScale.z * 1.15f);
                    crawler.localScale = Vector3.Lerp(crawler.localScale, targetScale, Time.deltaTime * 10f);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleCharacterEnter(other);
        }

        private void OnTriggerStay(Collider other)
        {
            var visuals = other.GetComponentInParent<PlayerRoleVisuals>();
            if (visuals != null && visuals.IsDad)
            {
                var motor = other.GetComponentInParent<CoreMovement>();
                if (motor != null && motor.IsOwner)
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

            var boost = controller.GetComponent<CoopBoostThrowAbility>();
            bool isDad = boost != null ? boost.IsDad : visuals != null ? visuals.IsDad : (other.name.ToLower().Contains("dad") || other.transform.root.name.ToLower().Contains("dad"));

            if (isDad)
            {
                if (motor != null && motor.IsOwner)
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
                m_ChildIsCrawling = true;

                // Hạ CharacterController
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

                // Tìm model mesh để ép dẹp scale thành tư thế khom bò (Crawl Pose)
                Transform visualTransform = GetVisualTransform(controller.gameObject);
                if (visualTransform != null && !m_OriginalModelScales.ContainsKey(visualTransform))
                {
                    m_OriginalModelScales[visualTransform] = visualTransform.localScale;
                    if (!m_ActiveCrawlers.Contains(visualTransform))
                    {
                        m_ActiveCrawlers.Add(visualTransform);
                    }
                }

                Debug.Log($"[CoopCrawlVolume] 👶 Con đang khom người bò qua khe hẹp '{gameObject.name}'!");
            }
        }

        private void HandleCharacterExit(Collider other)
        {
            var controller = other.GetComponentInParent<CharacterController>();
            if (controller == null) return;

            m_ChildIsCrawling = false;

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
            }

            Transform visualTransform = GetVisualTransform(controller.gameObject);
            if (visualTransform != null && m_OriginalModelScales.TryGetValue(visualTransform, out Vector3 origScale))
            {
                visualTransform.localScale = origScale;
                m_OriginalModelScales.Remove(visualTransform);
                m_ActiveCrawlers.Remove(visualTransform);
                Debug.Log($"[CoopCrawlVolume] 👶 Con đã chui ra ngoài an toàn và đứng thẳng dậy.");
            }
        }

        private Transform GetVisualTransform(GameObject root)
        {
            var visuals = root.GetComponent<PlayerRoleVisuals>();
            if (visuals != null && visuals.childModel != null)
            {
                return visuals.childModel.transform;
            }

            var anim = root.GetComponentInChildren<Animator>();
            if (anim != null) return anim.transform;

            if (root.transform.childCount > 0) return root.transform.GetChild(0);

            return root.transform;
        }

        private void OnDisable()
        {
            foreach (var entry in m_OriginalHeights)
                if (entry.Key != null)
                {
                    entry.Key.height = entry.Value;
                    if (m_OriginalCenters.TryGetValue(entry.Key, out var center)) entry.Key.center = center;
                }
            foreach (var entry in m_OriginalModelScales)
                if (entry.Key != null) entry.Key.localScale = entry.Value;
            m_OriginalHeights.Clear(); m_OriginalCenters.Clear(); m_OriginalModelScales.Clear(); m_ActiveCrawlers.Clear();
            m_ChildIsCrawling = false;
        }

        #endregion

        #region Debug UI & Gizmos

        private void OnGUI()
        {
            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            boxStyle.normal.textColor = Color.white;

            if (!string.IsNullOrEmpty(m_CurrentWarning))
            {
                GUI.color = new Color(1f, 0.25f, 0.25f, 0.95f);
                GUI.Box(new Rect(Screen.width / 2f - 280, 80, 560, 45), m_CurrentWarning, boxStyle);
            }
            else if (m_ChildIsCrawling)
            {
                GUI.color = new Color(0.2f, 0.9f, 0.5f, 0.9f);
                GUI.Box(new Rect(Screen.width / 2f - 220, 80, 440, 40), 
                    "👶 Bé đang bò chui qua khe hẹp...", boxStyle);
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
