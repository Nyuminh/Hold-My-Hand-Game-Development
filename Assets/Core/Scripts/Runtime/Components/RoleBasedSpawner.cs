zusing UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;

public class RoleBasedSpawner : MonoBehaviour
{
    [Tooltip("Kéo Prefab của nhân vật Dad vào đây")]
    public GameObject dadPrefab;

    [Tooltip("Kéo Prefab của nhân vật Child (BB CorePlayer) vào đây")]
    public GameObject childPrefab;

    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        }
    }

    private void OnServerStarted()
    {
        // Khi Server/Host bật lên, nó sẽ lắng nghe sự kiện có người kết nối vào
        // (Trong Unity Netcode, OnClientConnectedCallback cũng sẽ tự động kích hoạt cho chính Host khi Host tham gia)
        NetworkManager.Singleton.OnClientConnectedCallback += SpawnForClient;
    }

    private void SpawnForClient(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        // Đọc xem Host đang chọn Role gì trong Lobby
        bool hostIsDad = Lobby3DManager.AmIDad;
        bool isHost = (clientId == NetworkManager.Singleton.LocalClientId);
        
        // Logic đẻ nhân vật: 
        // Nếu Host chọn làm Dad -> Host được đẻ Dad, người vào sau (Client) đẻ Child.
        // Nếu Host chọn làm Child -> Host được đẻ Child, người vào sau (Client) đẻ Dad.
        bool shouldSpawnDad = (hostIsDad && isHost) || (!hostIsDad && !isHost);
        
        GameObject prefabToSpawn = shouldSpawnDad ? dadPrefab : childPrefab;
        if (prefabToSpawn != null)
        {
            var go = Instantiate(prefabToSpawn);
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
        }
        else
        {
            Debug.LogError("[RoleBasedSpawner] Lỗi! Bạn chưa kéo Prefab vào RoleBasedSpawner!");
        }
    }
}
