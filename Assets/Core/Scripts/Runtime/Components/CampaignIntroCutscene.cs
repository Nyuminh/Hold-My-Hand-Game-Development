using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Blocks.Gameplay.Core
{
    // The director belongs to the level, so respawning a player never replays the intro.
    public sealed class CampaignIntroCutscene : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (CampaignProgress.Level(scene.name) == 0) return;
            var director = new GameObject("Campaign Intro");
            SceneManager.MoveGameObjectToScene(director, scene);
            director.AddComponent<CampaignIntroCutscene>();
        }

        IEnumerator Start()
        {
            var scene = gameObject.scene;
            CorePlayerManager player;
            do
            {
                yield return null;
                player = LevelTwoActors.Local;
            } while (!player || player.gameObject.scene != scene || !player.IsSpawned ||
                     (GameManager.Instance && GameManager.Instance.EntryScreenVisible) ||
                     !player.CoreCamera || !player.CoreCamera.ActiveCameraMode ||
                     !Camera.main || !Camera.main.TryGetComponent<CinemachineBrain>(out _));

            // Each panorama ends looking along the playable route towards its landmark.
            Vector3 focus, from, to;
            string title;
            switch (scene.name)
            {
                case "PlayScene1": // Forest chase and the fallen tree
                    title = "Màn 1 · Đường rừng";
                    focus = new Vector3(130, 4, 48);
                    from = new Vector3(-22, 22, -31); to = new Vector3(6, 17, -26);
                    break;
                case "PlayScene2": // River approach to the sluice
                    title = "Màn 2 · Vượt thủy môn";
                    focus = new Vector3(100, 4, 74);
                    from = new Vector3(-11, 12, -40); to = new Vector3(8, 9, -31);
                    break;
                case "PlayScene4": // Drawbridge and the climb along the ramparts
                    title = "Màn 3 · Thành lũy trên cao";
                    focus = new Vector3(0, 21, 74);
                    from = new Vector3(84, 29, -62); to = new Vector3(56, 21, -53);
                    break;
                default: // Legacy coastal map
                    title = "Vịnh ven biển";
                    focus = player.transform.position + new Vector3(0, 3, 17);
                    from = new Vector3(-16, 13, -24); to = new Vector3(9, 12, -21);
                    break;
            }
            var intro = gameObject.AddComponent<TutorialIntroCutscene>();
            intro.PlayPanorama(player.NetworkObject, player.GetComponentInChildren<UIDocument>(),
                title, focus, focus + from, focus + to);
        }
    }
}
