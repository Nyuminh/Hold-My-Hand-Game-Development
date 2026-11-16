using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Điều khiển Bẫy Khối Gỗ Con Lắc (Pendulum Spiked Log Trap).
    /// Gồm sợi dây xích (Trap_Chain) và khối gỗ gai (Trap_SpikedLog) dính liền nhau thành một con lắc hoàn chỉnh.
    /// Chuyển động đung đưa theo phương ngang (trái - phải) giống hệt đồng hồ quả lắc,
    /// quét ngang qua cầu gỗ với vận tốc vừa phải và có khoảng dừng (delay) ở hai biên độ cao nhất
    /// để người chơi dễ dàng quan sát, canh nhịp và chạy vượt qua an toàn.
    /// </summary>
    [SelectionBase]
    public class PendulumTrapController : MonoBehaviour
    {
        [Header("Pendulum Hierarchy References")]
        [Tooltip("Transform của dây xích (Trap_Chain)")]
        [SerializeField] private Transform chainTransform;

        [Tooltip("Transform của khối gỗ gai (Trap_SpikedLog)")]
        [SerializeField] private Transform spikedLogTransform;

        [Header("Swing Motion Settings (Clock Pendulum Style)")]
        [Tooltip("Góc lắc tối đa sang hai bên (độ)")]
        [SerializeField] private float maxSwingAngle = 40f;

        [Tooltip("Thời gian để lắc từ một bên sang bên đối diện (giây)")]
        [SerializeField] private float swingHalfDuration = 2.2f;

        [Tooltip("Thời gian dừng/delay ở điểm cao nhất mỗi bên (giống đồng hồ quả lắc) để người chơi canh nhịp chạy qua (giây)")]
        [SerializeField] private float pauseAtPeaks = 0.6f;

        [Tooltip("Trục xoay của con lắc: hướng dọc theo đường cầu (0.768, 0, 0.64) để con lắc đung đưa theo phương ngang quét trái-phải")]
        [SerializeField] private Vector3 localSwingAxis = new Vector3(0.768f, 0f, 0.640f);

        [Header("Hazard / Damage Settings")]
        [Tooltip("Sát thương gây ra khi va chạm với người chơi")]
        [SerializeField] private float damage = 25f;

        [Tooltip("Lực đẩy lùi (Knockback Force) khi va chạm")]
        [SerializeField] private float knockbackForce = 12f;

        [Tooltip("Thời gian hồi miễn nhiễm va chạm cho mỗi người chơi (giây)")]
        [SerializeField] private float hitCooldown = 1.0f;

        private Quaternion initialLocalRotation;
        private float currentTimer = 0f;
        private float currentAngle = 0f;
        private float previousAngle = 0f;
        private float angularVelocity = 0f;
        private readonly Dictionary<Collider, float> lastHitTimes = new Dictionary<Collider, float>();

        private void Awake()
        {
            initialLocalRotation = transform.localRotation;
            LinkPendulumParts();
            SetupHazardColliders();
        }

        private void Start()
        {
            initialLocalRotation = transform.localRotation;
            LinkPendulumParts();
            SetupHazardColliders();
        }

        /// <summary>
        /// Gắn kết dính liền sợi dây và khối gỗ vào Pivot để tạo thành 1 con lắc thống nhất.
        /// </summary>
        [ContextMenu("Link Pendulum Parts Now")]
        public void LinkPendulumParts()
        {
            if (chainTransform == null)
            {
                var c = transform.Find("Trap_Chain");
                if (c == null) c = GameObject.Find("Trap_Chain")?.transform;
                if (c != null) chainTransform = c;
            }

            if (spikedLogTransform == null)
            {
                var l = transform.Find("Trap_SpikedLog");
                if (l == null) l = GameObject.Find("Trap_SpikedLog")?.transform;
                if (l != null) spikedLogTransform = l;
            }

            // Gắn làm con của Pivot (giữ nguyên vị trí thế giới để không bị lệch)
            if (chainTransform != null && chainTransform.parent != transform)
            {
                chainTransform.SetParent(transform, true);
            }

            if (spikedLogTransform != null && spikedLogTransform.parent != transform)
            {
                spikedLogTransform.SetParent(transform, true);
            }
        }

        public void SetupHazardColliders()
        {
            if (spikedLogTransform == null) return;

            // Đảm bảo có Rigidbody kinematic để trigger tương tác vật lý chuẩn xác
            var rb = spikedLogTransform.GetComponent<Rigidbody>();
            if (rb == null) rb = spikedLogTransform.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Gắn component hazard receiver để chuyển tiếp sự kiện trigger về controller
            var hazardTrigger = spikedLogTransform.GetComponent<PendulumHazardTrigger>();
            if (hazardTrigger == null)
            {
                hazardTrigger = spikedLogTransform.gameObject.AddComponent<PendulumHazardTrigger>();
            }
            hazardTrigger.Initialize(this);

            // Bật Trigger cho MeshCollider hoặc thêm CapsuleCollider
            var meshCollider = spikedLogTransform.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                meshCollider.convex = true;
                meshCollider.isTrigger = true;
            }

            var cap = spikedLogTransform.GetComponent<CapsuleCollider>();
            if (cap == null) cap = spikedLogTransform.gameObject.AddComponent<CapsuleCollider>();
            cap.radius = 0.58f;
            cap.height = 3.3f;
            cap.direction = 1; // Y axis (dọc theo chiều dài thân gỗ)
            cap.isTrigger = true;
        }

        private void Update()
        {
            UpdatePendulumMotion();
        }

        /// <summary>
        /// Tính toán góc lắc con lắc điều hòa có khoảng dừng (delay) ở hai biên độ (trái và phải).
        /// </summary>
        private void UpdatePendulumMotion()
        {
            float totalHalfCycle = swingHalfDuration + pauseAtPeaks;
            float fullCycle = totalHalfCycle * 2f;

            currentTimer += Time.deltaTime;
            float cycleTime = currentTimer % fullCycle;

            float targetAngle;

            if (cycleTime < swingHalfDuration)
            {
                // Giai đoạn 1: Lắc từ Trái (-maxSwingAngle) sang Phải (+maxSwingAngle)
                float t = cycleTime / swingHalfDuration;
                // Dao động điều hòa êm ái (Harmonic Easing): chậm ở 2 đỉnh, nhanh nhất khi quét qua giữa cầu
                float ease = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI);
                targetAngle = Mathf.Lerp(-maxSwingAngle, maxSwingAngle, ease);
            }
            else if (cycleTime < totalHalfCycle)
            {
                // Giai đoạn 2: Dừng (delay) ở đỉnh bên phải (+maxSwingAngle) để người chơi canh nhịp chạy qua
                targetAngle = maxSwingAngle;
            }
            else if (cycleTime < totalHalfCycle + swingHalfDuration)
            {
                // Giai đoạn 3: Lắc ngược lại từ Phải (+maxSwingAngle) về Trái (-maxSwingAngle)
                float t = (cycleTime - totalHalfCycle) / swingHalfDuration;
                float ease = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI);
                targetAngle = Mathf.Lerp(maxSwingAngle, -maxSwingAngle, ease);
            }
            else
            {
                // Giai đoạn 4: Dừng (delay) ở đỉnh bên trái (-maxSwingAngle)
                targetAngle = -maxSwingAngle;
            }

            previousAngle = currentAngle;
            currentAngle = targetAngle;
            if (Time.deltaTime > 0.0001f)
            {
                angularVelocity = (currentAngle - previousAngle) / Time.deltaTime;
            }

            // Áp dụng góc xoay mượt mà quanh trục lắc ngang (theo hướng cầu)
            Vector3 axis = localSwingAxis.sqrMagnitude > 0.001f ? localSwingAxis.normalized : new Vector3(0.768f, 0f, 0.640f);
            transform.localRotation = initialLocalRotation * Quaternion.AngleAxis(currentAngle, axis);
        }

        /// <summary>
        /// Xử lý va chạm khi khối gỗ quét trúng người chơi.
        /// </summary>
        public void ProcessHazardHit(Collider other)
        {
            if (other == null) return;

            // Kiểm tra cooldown tránh đánh nhiều lần liên tục
            if (lastHitTimes.TryGetValue(other, out float lastTime))
            {
                if (Time.time - lastTime < hitCooldown) return;
            }
            lastHitTimes[other] = Time.time;

            // Tính hướng đẩy lùi (Knockback direction)
            Vector3 hitPoint;
            if (other is MeshCollider mc && !mc.convex)
            {
                hitPoint = other.bounds.ClosestPoint(spikedLogTransform != null ? spikedLogTransform.position : transform.position);
            }
            else
            {
                hitPoint = other.ClosestPoint(spikedLogTransform != null ? spikedLogTransform.position : transform.position);
            }
            Vector3 pushDir = (other.bounds.center - hitPoint);
            pushDir.y = 0.35f; // Hất nhẹ lên trên
            if (pushDir.sqrMagnitude < 0.01f)
            {
                pushDir = -transform.forward + Vector3.up * 0.35f;
            }
            pushDir.Normalize();

            // 1. Gọi hệ thống IHittable nếu đối tượng có IHittable
            var hittable = other.GetComponent<IHittable>() ?? other.GetComponentInParent<IHittable>();
            if (hittable != null)
            {
                HitInfo info = new HitInfo
                {
                    amount = damage,
                    hitPoint = hitPoint,
                    impactForce = pushDir * knockbackForce
                };
                hittable.OnHit(info);
            }

            // 2. Tác động trực tiếp lên Rigidbody của người chơi
            var rb = other.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                rb.AddForce(pushDir * knockbackForce, ForceMode.Impulse);
            }

            // 3. Tác động lên CharacterController
            var cc = other.GetComponent<CharacterController>() ?? other.GetComponentInParent<CharacterController>();
            if (cc != null)
            {
                cc.Move(pushDir * (knockbackForce * 0.25f));
            }
        }

        [ContextMenu("Test Swing Left (Trái)")]
        public void TestSwingLeft()
        {
            Vector3 axis = localSwingAxis.sqrMagnitude > 0.001f ? localSwingAxis.normalized : new Vector3(0.768f, 0f, 0.640f);
            transform.localRotation = initialLocalRotation * Quaternion.AngleAxis(-maxSwingAngle, axis);
        }

        [ContextMenu("Test Swing Right (Phải)")]
        public void TestSwingRight()
        {
            Vector3 axis = localSwingAxis.sqrMagnitude > 0.001f ? localSwingAxis.normalized : new Vector3(0.768f, 0f, 0.640f);
            transform.localRotation = initialLocalRotation * Quaternion.AngleAxis(maxSwingAngle, axis);
        }

        [ContextMenu("Reset Rotation (Giữa - 0 deg)")]
        public void ResetRotation()
        {
            transform.localRotation = initialLocalRotation;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 axis = localSwingAxis.sqrMagnitude > 0.001f ? localSwingAxis.normalized : new Vector3(0.768f, 0f, 0.640f);
            Vector3 worldAxis = transform.TransformDirection(axis);
            Gizmos.DrawRay(transform.position - worldAxis * 3f, worldAxis * 6f);

            if (spikedLogTransform != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(spikedLogTransform.position, 0.6f);
            }
        }
    }

    /// <summary>
    /// Component nhận diện sự kiện Trigger trên khối gỗ và chuyển tiếp tới PendulumTrapController.
    /// </summary>
    public class PendulumHazardTrigger : MonoBehaviour
    {
        private PendulumTrapController controller;

        public void Initialize(PendulumTrapController ctrl)
        {
            controller = ctrl;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (controller != null)
            {
                controller.ProcessHazardHit(other);
            }
        }
    }
}
