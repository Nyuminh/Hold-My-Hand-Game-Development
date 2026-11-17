using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Khởi tạo và bảo đảm môi trường Co-op cho PlayScene3:
    /// 1. Tự động dịch chuyển Hộp Điện Zone 3 (Zone3_Electrical_Box) lên mặt sàn gỗ thoáng đãng,
    ///    tránh hoàn toàn va chạm hoặc bị vách đá che lấp.
    /// 2. Điều chỉnh tảng đá Pushable_Boulder để lối đi xuyên qua cao nguyên luôn thông suốt.
    /// 3. Kích hoạt Bộ Chỉ Hướng Mục Tiêu & La Bàn (PlayScene3ObjectiveDirector).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayScene3CoopBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoaded()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.name != "PlayScene3") return;

            var existing = FindAnyObjectByType<PlayScene3CoopBootstrap>();
            if (existing == null)
            {
                var go = new GameObject("[PlayScene3_Coop_Bootstrap]");
                go.AddComponent<PlayScene3CoopBootstrap>();
            }
        }

        private void Awake()
        {
            FixObstructions();
            EnsureObjectiveDirector();
        }

        private void Start()
        {
            FixObstructions();
        }

        private void FixObstructions()
        {
            // 1. Hộp điện Zone 3: đặt lên bờ sàn gỗ thông thoáng
            var electricBox = GameObject.Find("Zone3_Electrical_Box");
            if (electricBox != null)
            {
                electricBox.transform.position = new Vector3(0.15f, 14.35f, -45.50f);
            }

            // 2. Chốt tời Zone 3
            var z3Lever = GameObject.Find("Zone3_Return_Lever");
            if (z3Lever != null)
            {
                z3Lever.transform.position = new Vector3(0.50f, 14.20f, -45.50f);
            }

            // 3. Tảng đá Pushable_Boulder: đặt hơi lệch sang bên lề để không chắn lối đi
            var boulder = GameObject.Find("Pushable_Boulder");
            if (boulder != null)
            {
                boulder.transform.position = new Vector3(-4.20f, 15.40f, -54.00f);
                var sphereCol = boulder.GetComponent<SphereCollider>();
                if (sphereCol != null)
                {
                    sphereCol.radius = 0.55f;
                }
            }
        }

        private void EnsureObjectiveDirector()
        {
            if (FindAnyObjectByType<PlayScene3ObjectiveDirector>() == null)
            {
                var directorGo = new GameObject("PlayScene3_ObjectiveDirector");
                directorGo.AddComponent<PlayScene3ObjectiveDirector>();
                Debug.Log("<color=cyan>[PlayScene3] Đã kích hoạt Bộ Chỉ Hướng Mục Tiêu & La Bàn Dẫn Đường!</color>");
            }
        }
    }
}
