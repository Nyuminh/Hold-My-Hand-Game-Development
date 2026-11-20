using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>Survives the encounter scene so existing clients receive fresh level managers and role spawns.</summary>
    public sealed class LevelOneSceneTransition : MonoBehaviour
    {
        private NetworkManager network;
        private RoleBasedSpawner spawner;
        private string target;
        private float deadline;
        private bool prepared, completed;

        public void Begin(NetworkManager manager,string sceneName)
        {
            network=manager;target=sceneName;deadline=Time.unscaledTime+120;
            spawner=manager.GetComponent<RoleBasedSpawner>();
            network.SceneManager.OnSceneEvent+=OnSceneEvent;
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            if(sceneEvent.SceneName!=target)return;
            if(sceneEvent.SceneEventType==SceneEventType.Load && !prepared)
            {
                prepared=true;
                if(spawner!=null)spawner.enabled=false;
                if(GameManager.Instance!=null)Destroy(GameManager.Instance.gameObject);
            }
            if(sceneEvent.SceneEventType==SceneEventType.LoadEventCompleted && !completed)
            {
                completed=true;
                if(network.IsServer && spawner!=null)
                {
                    RoleBasedSpawner.ClearSpawnedClients();
                    spawner.enabled=true;
                    foreach(var client in network.ConnectedClientsList)spawner.SpawnForClient(client.ClientId);
                }
                // Every peer must set up its destination UI, including clients that stay
                // in the same Multiplayer Services session and receive no SessionAdded.
                StartCoroutine(ResumeLocalPlayer());
            }
        }
        private System.Collections.IEnumerator ResumeLocalPlayer()
        {
            while(network != null && network.IsListening && Time.unscaledTime < deadline)
            {
                if(GameManager.Instance != null && network.LocalClient?.PlayerObject != null && network.LocalClient.PlayerObject.IsSpawned)
                {
                    GameManager.Instance.ResumeAfterLevelTransition();
                    if (target == "PlayScene4") CampaignProgress.Save(3, 0);
                    // Scene-authored NetworkManagers are dormant while the existing
                    // session survives. Remove duplicates before another transition.
                    foreach(var other in FindObjectsByType<NetworkManager>())
                        if(other != network && !other.IsListening)Destroy(other.gameObject);
                    Destroy(this);
                    yield break;
                }
                yield return null;
            }
        }
        private void Update(){if(Time.unscaledTime>deadline)Destroy(this);}
        private void OnDestroy(){if(network!=null&&network.SceneManager!=null)network.SceneManager.OnSceneEvent-=OnSceneEvent;}
    }
}
