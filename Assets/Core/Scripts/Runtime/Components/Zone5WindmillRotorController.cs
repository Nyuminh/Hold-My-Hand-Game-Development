using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Điều khiển cối xay gió xoay tròn Zone 5 (Zone5_Windmill_Rotor).
    /// Các cánh cối xay gió xoay đều quanh trục X để tạo thử thách canh thời gian:
    /// Cha con nhảy lên đệm bật nhảy, canh cánh quạt cối xay xoay ngang để nhảy lên cánh
    /// hoặc nhảy xuyên qua khoảng trống sang đài quan sát SkyDock Landing!
    /// Hoạt động mượt mà cả offline lẫn đồng bộ mạng Netcode.
    /// </summary>
    public class Zone5WindmillRotorController : NetworkBehaviour
    {
        [Header("Rotation Settings")]
        [Tooltip("Tốc độ quay (độ / giây). Khoảng 20-30 độ/s là tốc độ chuẩn vừa đẹp vừa dễ nhảy")]
        [SerializeField] private float rotationSpeed = 22.5f;

        [Tooltip("Trục xoay trong không gian thế giới (World space)")]
        [SerializeField] private Vector3 rotationAxis = Vector3.right;

        [Header("Player Transport (Moving Platform Support)")]
        [SerializeField] private bool syncNetworkTime = true;

        private Quaternion initialRotation;

        private void Awake()
        {
            initialRotation = transform.rotation;
        }

        private void Update()
        {
            float angle = 0f;
            if (syncNetworkTime && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                // Đồng bộ chính xác theo Network Time cho cả 2 người chơi
                angle = (float)((NetworkManager.Singleton.ServerTime.Time * rotationSpeed) % 360.0);
            }
            else
            {
                // Offline / Solo fallback
                angle = (Time.time * rotationSpeed) % 360f;
            }

            transform.rotation = initialRotation * Quaternion.AngleAxis(angle, rotationAxis);
        }

        // Hỗ trợ giữ nhân vật đứng trên cánh quạt khi cối xay quay
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.collider.CompareTag("Player"))
            {
                collision.transform.SetParent(transform, true);
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision.collider.CompareTag("Player"))
            {
                if (collision.transform.parent == transform)
                {
                    collision.transform.SetParent(null, true);
                }
            }
        }
    }
}
