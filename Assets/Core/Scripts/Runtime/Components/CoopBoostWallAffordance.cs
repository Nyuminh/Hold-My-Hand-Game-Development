using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Gợi ý Môi trường & Hỗ trợ Ném lên Tường Cao (Visual Affordance & Boost Assist):
    /// Dành cho Màn 2: "Đôi Cánh Của Cha"
    /// - Khi Ba đứng trước bờ tường cao 3.2m:
    ///   + Tự động chiếu luồng sáng vàng hổ phách dịu nhẹ lên gờ tường mục tiêu.
    ///   + Hỗ trợ tinh chỉnh góc ném Parabol của CoopBoostThrowAbility để Con tiếp đất chuẩn xác trên đỉnh gờ tường.
    /// </summary>
    public class CoopBoostWallAffordance : MonoBehaviour
    {
        [Header("Mục tiêu Đỉnh Tường")]
        [Tooltip("Điểm tiếp đất trên đỉnh bờ tường")]
        [SerializeField] private Transform rampartTopPoint;

        [Tooltip("Thang dây nối liền với bức tường này")]
        [SerializeField] private CoopRopeLadderDrop ropeLadder;

        [Header("Gợi ý Thị giác")]
        [Tooltip("Đèn rọi điểm tiếp đất trên đỉnh tường")]
        [SerializeField] private Light targetSpotlight;

        [Tooltip("Khoảng cách kích hoạt gợi ý khi Ba đứng gần chân tường (mét)")]
        [SerializeField] private float triggerDistance = 6.0f;

        private void Start()
        {
            if (targetSpotlight != null)
            {
                targetSpotlight.color = new Color(1f, 0.85f, 0.4f);
                targetSpotlight.intensity = 1.6f;
            }
        }

        private void Update()
        {
            // Kiểm tra xem thang đã rủ xuống chưa, nếu đã rủ xuống thì tắt đèn rọi
            if (ropeLadder != null && ropeLadder.IsLadderDropped)
            {
                if (targetSpotlight != null && targetSpotlight.enabled)
                {
                    targetSpotlight.enabled = false;
                }
                return;
            }

            // Tìm nhân vật Ba trong bán kính triggerDistance
            bool dadNearby = false;
            foreach (var p in PlayerRoleVisuals.AllPlayers)
            {
                if (p != null && p.IsDad)
                {
                    float d = Vector3.Distance(transform.position, p.transform.position);
                    if (d <= triggerDistance)
                    {
                        dadNearby = true;
                        break;
                    }
                }
            }

            if (targetSpotlight != null)
            {
                targetSpotlight.enabled = dadNearby;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, triggerDistance);
            if (rampartTopPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, rampartTopPoint.position);
                Gizmos.DrawSphere(rampartTopPoint.position, 0.35f);
            }
        }
#endif
    }
}
