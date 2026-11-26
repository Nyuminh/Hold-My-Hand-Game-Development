using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Vùng kích hoạt góc nhìn Camera Nội Thất (Indoor Camera Zone).
    /// Khi nhân vật bước vào lâu đài/hầm/phòng kín, tự động thu gọn cự ly camera
    /// và mở rộng FOV để tránh va chạm cột/tường và chiêm ngưỡng kiến trúc tráng lệ.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class IndoorCameraZone : MonoBehaviour
    {
        [Header("Indoor Framing Tuning")]
        [Tooltip("Cự ly camera của Ba khi ở trong nhà (mét)")]
        [SerializeField] private float dadIndoorDistance = 3.2f;

        [Tooltip("Cự ly camera của Con khi ở trong nhà (mét)")]
        [SerializeField] private float childIndoorDistance = 2.4f;

        [Tooltip("Độ mở ống kính FOV trong nhà (độ)")]
        [SerializeField] private float indoorFOV = 68.0f;

        private void Reset()
        {
            var col = GetComponent<BoxCollider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var camCtrl = other.GetComponent<CoreCameraController>() ?? other.GetComponentInParent<CoreCameraController>();
            if (camCtrl != null)
            {
                camCtrl.SetIndoorOverride(true, dadIndoorDistance, childIndoorDistance, indoorFOV);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var camCtrl = other.GetComponent<CoreCameraController>() ?? other.GetComponentInParent<CoreCameraController>();
            if (camCtrl != null)
            {
                camCtrl.SetIndoorOverride(false);
            }
        }
    }
}
