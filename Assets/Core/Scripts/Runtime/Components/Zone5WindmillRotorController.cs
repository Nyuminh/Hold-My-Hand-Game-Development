using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Điều khiển cối xay gió xoay tròn Zone 5 (Zone5_Windmill_Rotor).
    /// Các cánh cối xay gió có gắn các bục đứng (Zone5_Windmill_BladeTip_0..3) luôn tự cân bằng mặt phẳng ngang
    /// giống cabin đu quay (Ferris-wheel Gondola) để người chơi nhảy từ Zone5_Windmill_Landing lên cục đứng,
    /// được cối xay xoay nâng lên đỉnh và nhảy tiếp sang Zone5_SkyDock_Landing!
    /// Hỗ trợ kéo theo CharacterController mượt mà mà không cần Rigidbody va chạm.
    /// </summary>
    public class Zone5WindmillRotorController : NetworkBehaviour
    {
        [Header("Rotation Settings")]
        [Tooltip("Tốc độ quay (độ / giây). Khoảng 20-30 độ/s là tốc độ chuẩn vừa đẹp vừa dễ nhảy")]
        [SerializeField] private float rotationSpeed = 22.5f;

        [Tooltip("Trục xoay trong không gian thế giới (World space)")]
        [SerializeField] private Vector3 rotationAxis = Vector3.right;

        [Header("Gondola Platforms (Keep Upright & Carry Players)")]
        [Tooltip("Giữ cho các bục đứng ở đầu cánh quạt (BladeTip_0..3) luôn nằm ngang để người chơi đứng vững")]
        [SerializeField] private bool keepBladeTipsUpright = true;

        [Tooltip("Danh sách các bục đứng gắn ở đầu cánh quạt")]
        [SerializeField] private List<Transform> bladeTipPlatforms = new List<Transform>();

        [Header("Player Transport (Moving Platform Support)")]
        [SerializeField] private bool syncNetworkTime = true;

        private Quaternion initialRotation;
        private readonly Dictionary<Transform, Vector3> m_PrevPlatformPositions = new Dictionary<Transform, Vector3>();

        private void Awake()
        {
            initialRotation = transform.rotation;
            AutoFindBladeTips();
        }

        private void AutoFindBladeTips()
        {
            if (bladeTipPlatforms.Count == 0)
            {
                foreach (Transform ch in GetComponentsInChildren<Transform>(false))
                {
                    if (ch != transform && (ch.name.Contains("BladeTip") || ch.name.Contains("PlatformPod")))
                    {
                        bladeTipPlatforms.Add(ch);
                    }
                }
            }

            m_PrevPlatformPositions.Clear();
            foreach (var tip in bladeTipPlatforms)
            {
                if (tip != null)
                {
                    if (keepBladeTipsUpright) tip.rotation = Quaternion.identity;
                    m_PrevPlatformPositions[tip] = tip.position;
                }
            }
        }

        private void Update()
        {
            float angle = 0f;
            if (syncNetworkTime && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                angle = (float)((NetworkManager.Singleton.ServerTime.Time * rotationSpeed) % 360.0);
            }
            else
            {
                angle = (Time.time * rotationSpeed) % 360f;
            }

            transform.rotation = initialRotation * Quaternion.AngleAxis(angle, rotationAxis);

            // Giữ các bục đứng (BladeTip_0..3) luôn phẳng ngang và kéo theo người chơi đang đứng trên bục!
            for (int i = 0; i < bladeTipPlatforms.Count; i++)
            {
                Transform tip = bladeTipPlatforms[i];
                if (tip == null || !tip.gameObject.activeInHierarchy) continue;

                if (keepBladeTipsUpright)
                {
                    tip.rotation = Quaternion.identity;
                }

                Vector3 currentPos = tip.position;
                if (m_PrevPlatformPositions.TryGetValue(tip, out Vector3 prevPos))
                {
                    Vector3 delta = currentPos - prevPos;
                    if (delta.sqrMagnitude > 0.000001f)
                    {
                        CarryPlayersOnPlatform(tip, delta);
                    }
                }
                m_PrevPlatformPositions[tip] = currentPos;
            }
        }

        private void CarryPlayersOnPlatform(Transform platform, Vector3 delta)
        {
            // Kiểm tra vùng hộp ngay trên mặt bục để kéo theo mọi CharacterController đang đứng trên đó
            Vector3 boxCenter = platform.position + Vector3.up * 0.95f;
            var rend = platform.GetComponent<Renderer>();
            Vector3 halfExtents = new Vector3(1.05f, 1.15f, 1.05f);
            if (rend != null)
            {
                boxCenter = new Vector3(rend.bounds.center.x, rend.bounds.max.y + 0.65f, rend.bounds.center.z);
                halfExtents = new Vector3(
                    Mathf.Max(0.85f, rend.bounds.extents.x * 0.95f),
                    0.85f,
                    Mathf.Max(0.85f, rend.bounds.extents.z * 0.95f)
                );
            }

            Collider[] hits = Physics.OverlapBox(boxCenter, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var cc = hits[i].GetComponent<CharacterController>();
                if (cc == null) cc = hits[i].GetComponentInParent<CharacterController>();
                if (cc != null && cc.enabled)
                {
                    cc.Move(delta);
                }
            }
        }
    }
}
