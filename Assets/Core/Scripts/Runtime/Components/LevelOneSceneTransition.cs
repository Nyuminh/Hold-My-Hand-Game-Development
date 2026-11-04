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

        public void Begin(NetworkManager manager,string sceneName)
        {
            network=manager;target=sceneName;deadline=Time.unscaledTime+120;
            spawner=manager.GetComponent<RoleBasedSpawner>();
            network.SceneManager.OnSceneEvent+=OnSceneEvent;
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            if(sceneEvent.SceneName!=target)return;
            if(sceneEvent.SceneEventType==SceneEventType.Load)
            {
                if(spawner!=null)spawner.enabled=false;
                if(GameManager.Instance!=null)Destroy(GameManager.Instance.gameObject);
            }
            if(sceneEvent.SceneEventType==SceneEventType.LoadEventCompleted)
            {
                if(network.IsServer && spawner!=null)
                {
                    RoleBasedSpawner.ClearSpawnedClients();
                    spawner.enabled=true;
                    foreach(var client in network.ConnectedClientsList)spawner.SpawnForClient(client.ClientId);
                }
                Destroy(this);
            }
        }
        private void Update(){if(Time.unscaledTime>deadline)Destroy(this);}
        private void OnDestroy(){if(network!=null&&network.SceneManager!=null)network.SceneManager.OnSceneEvent-=OnSceneEvent;}
    }
}
