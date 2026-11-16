using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Vùng Thử Thách Tâm Lý (Hesitation Hazard Zone):
    /// Đặt trước các khu vực nguy hiểm (cầu gỗ gãy, vực sâu sương mù, hang tối).
    /// - Khi Đứa Con đi một mình tới đây mà CHƯA nắm tay Ba:
    ///   + Con sẽ sợ hãi, chần chừ, bước chân chậm lại (tốc độ giảm xuống 25%).
    ///   + Hiện tín hiệu gọi Ba (Child needs Dad's hand).
    /// - Khi Ba tới nắm tay Con:
    ///   + Nỗi sợ tan biến, tốc độ hồi phục, hai cha con cùng sải bước qua an toàn!
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HesitationHazardZone : MonoBehaviour
    {
        [Header("Thông điệp & Cảm xúc")]
        [Tooltip("Tên vùng nguy hiểm (ví dụ: Cầu gỗ gãy qua vực sương mù)")]
        [SerializeField] private string hazardName = "Cầu Gỗ Mục";

        [Tooltip("Hệ số làm chậm khi Con sợ hãi (0.25 = đi chậm 25% tốc độ thường)")]
        [SerializeField] private float fearSpeedMultiplier = 0.25f;

        [Tooltip("Bán kính nhận diện Ba ở gần (mét)")]
        [SerializeField] private float dadComfortRadius = 3.5f;

        public float FearSpeedMultiplier => fearSpeedMultiplier;
        public float DadComfortRadius => dadComfortRadius;
        public string HazardName => hazardName;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<ChildFearHesitation>(out var hesitation))
            {
                hesitation.EnterHazardZone(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<ChildFearHesitation>(out var hesitation))
            {
                hesitation.ExitHazardZone(this);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.45f, 0.2f, 0.35f);
            var col = GetComponent<Collider>();
            if (col != null)
            {
                Gizmos.DrawCube(col.bounds.center, col.bounds.size);
                Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.85f);
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            }
        }
#endif
    }
}
