using System.Collections;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Thang Dây Cuộn Tròn Thả Xuống (Coop Droppable Rope Ladder):
    /// Dành cho Màn 2: "Đôi Cánh Của Cha"
    /// - Ban đầu: Thang cuộn tròn trên đỉnh bờ tường cao 3.2m (Ba không trèo lên được).
    /// - Khi Con được Ba kiệu vai ném bổng lên đỉnh tường [F]:
    ///   + Con chạy lại gần đạp chân vào chốt giữ (hoặc bấm [F]).
    ///   + Cuộn thang bung xõa rủ thẳng xuống chân tường chạm đất (Smooth Drop Animation).
    ///   + Kích hoạt lối leo / dốc hỗ trợ để người Cha ở dưới chân tường leo lên đoàn tụ với Con.
    /// </summary>
    public class CoopRopeLadderDrop : MonoBehaviour
    {
        [Header("Trạng thái Thang")]
        [SerializeField] private bool isDropped = false;

        [Header("Tham chiếu Đối tượng")]
        [Tooltip("Phần thang dây bung xuống (ban đầu ẩn hoặc ở vị trí cuộn trên đỉnh)")]
        [SerializeField] private GameObject unfoldedLadderVisual;

        [Tooltip("Cuộn thang dây khi còn cuộn tròn (ẩn đi khi bung)")]
        [SerializeField] private GameObject coiledLadderVisual;

        [Tooltip("Collider hoặc dốc leo hỗ trợ khi thang đã thả xuống")]
        [SerializeField] private Collider climbRampCollider;

        [Tooltip("Đèn báo / Vạch sáng màu vàng hổ phách mách nước (Visual Affordance)")]
        [SerializeField] private Light guideLight;

        [Header("Tương tác & Hiệu ứng")]
        [SerializeField] private float dropDuration = 0.85f;
        [SerializeField] private KeyCode kickKey = KeyCode.F;

        public bool IsLadderDropped => isDropped;

        private bool m_IsAnimating = false;

        private void Awake()
        {
            UpdateVisualStates(immediate: true);
        }

        private void UpdateVisualStates(bool immediate)
        {
            if (isDropped)
            {
                if (coiledLadderVisual != null) coiledLadderVisual.SetActive(false);
                if (unfoldedLadderVisual != null) unfoldedLadderVisual.SetActive(true);
                if (climbRampCollider != null) climbRampCollider.enabled = true;
                if (guideLight != null) guideLight.color = new Color(0.3f, 1f, 0.5f); // Đã thông đường (xanh)
            }
            else
            {
                if (coiledLadderVisual != null) coiledLadderVisual.SetActive(true);
                if (unfoldedLadderVisual != null) unfoldedLadderVisual.SetActive(false);
                if (climbRampCollider != null) climbRampCollider.enabled = false;
                if (guideLight != null) guideLight.color = new Color(1f, 0.82f, 0.35f); // Đang chờ (vàng ấm)
            }
        }

        /// <summary>
        /// Kích hoạt đạp rơi thang dây xuống đất.
        /// </summary>
        public void DropLadder()
        {
            if (isDropped || m_IsAnimating) return;
            StartCoroutine(DropRoutine());
        }

        private IEnumerator DropRoutine()
        {
            m_IsAnimating = true;

            // Ẩn cuộn thang
            if (coiledLadderVisual != null) coiledLadderVisual.SetActive(false);

            if (unfoldedLadderVisual != null)
            {
                unfoldedLadderVisual.SetActive(true);
                Vector3 originalScale = unfoldedLadderVisual.transform.localScale;
                Vector3 startScale = new Vector3(originalScale.x, 0.05f, originalScale.z);
                unfoldedLadderVisual.transform.localScale = startScale;

                float elapsed = 0f;
                while (elapsed < dropDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / dropDuration);
                    // Hiệu ứng nảy nhẹ khi thang chạm đất (Bounce Out)
                    float scaleY = Mathf.Lerp(0.05f, originalScale.y, t);
                    unfoldedLadderVisual.transform.localScale = new Vector3(originalScale.x, scaleY, originalScale.z);
                    yield return null;
                }

                unfoldedLadderVisual.transform.localScale = originalScale;
            }

            isDropped = true;
            m_IsAnimating = false;
            UpdateVisualStates(immediate: true);

            Debug.Log("[CoopRopeLadderDrop] ✅ Đứa Con đã đạp thang dây rủ xuống đất! Ba có thể leo lên.");
        }

        private void OnTriggerStay(Collider other)
        {
            if (isDropped || m_IsAnimating) return;

            // Kiểm tra xem có phải Đứa Con đã lên đỉnh tường không
            if (other.TryGetComponent<PlayerRoleVisuals>(out var role))
            {
                if (!role.IsDad)
                {
                    // Con chỉ cần chạy lại gần hoặc bấm phím là thang tự bung
                    DropLadder();
                }
            }
            else if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                // Fallback nếu không có role: bấm phím F hoặc chạm vào cuộn thang là rớt
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame)
                {
                    DropLadder();
                }
                else
#endif
                if (Input.GetKey(kickKey))
                {
                    DropLadder();
                }
                else
                {
                    // Chạm trực tiếp vào cuộn thang trên đỉnh tường
                    DropLadder();
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = isDropped ? Color.green : new Color(1f, 0.8f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(transform.position, new Vector3(1.2f, 0.6f, 1.2f));
        }
#endif
    }
}
