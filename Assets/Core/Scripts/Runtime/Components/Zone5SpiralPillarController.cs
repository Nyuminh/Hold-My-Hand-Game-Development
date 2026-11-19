using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Cơ chế Bậc Thang Trụ Đá Thần Bí (Zone 5 Spiral Rune Pillars Puzzle).
    /// Bậc thang xoắn ốc dẫn từ cổng sau lâu đài lên SkyDock.
    /// Có các bệ Rune Mặt Trời (Ba) và Ánh Trăng (Con).
    /// Khi Ba kích hoạt bệ, các bậc thang gỗ phụ trên thân trụ đá sẽ xoay / mở rộng ra
    /// để Con và Ba có thể dễ dàng chạy thẳng lên đài quan sát SkyDock mà không bị rơi!
    /// </summary>
    public class Zone5SpiralPillarController : MonoBehaviour
    {
        [Header("Pillars and Step Lights")]
        [SerializeField] private Light[] pillarCapLights;
        [SerializeField] private Color activeCapColor = new Color(0.2f, 0.9f, 1f, 1f);

        [Header("Smooth Ramp Assistance")]
        [Tooltip("Collider dốc hỗ trợ chạy bộ mượt mà không lo trượt ngã")]
        [SerializeField] private GameObject walkableBridgeRamp;

        private void Awake()
        {
            EnsureWalkableSurfaces();
        }

        public void EnsureWalkableSurfaces()
        {
            // Đảm bảo tất cả các bậc thang Zone5_RuneStair đều có collider chắc chắn
            for (int p = 0; p <= 4; p++)
            {
                for (int s = 0; s <= 7; s++)
                {
                    string stepName = $"Zone5_RuneStair_P{p}_{s:D2}";
                    var stepObj = GameObject.Find(stepName);
                    if (stepObj != null)
                    {
                        var col = stepObj.GetComponent<Collider>();
                        if (col == null)
                        {
                            var box = stepObj.AddComponent<BoxCollider>();
                            box.size = new Vector3(1.6f, 0.4f, 1.2f);
                        }
                    }
                }
            }
        }
    }
}
