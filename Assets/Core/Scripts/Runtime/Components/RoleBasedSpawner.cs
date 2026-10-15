using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;

public class RoleBasedSpawner : MonoBehaviour
{
    [Tooltip("Kéo Prefab của nhân vật Dad vào đây")]
    public GameObject dadPrefab;

    [Tooltip("Kéo Prefab của nhân vật Child (BB CorePlayer) vào đây")]
    public GameObject childPrefab;

    // Theo dõi danh sách clientId đã được spawn để ngăn chặn nhân bản tuyệt đối
    private static readonly HashSet<ulong> s_SpawnedClients = new HashSet<ulong>();

    private void Awake()
    {
        s_SpawnedClients.Clear();
    }

    private void OnEnable()
    {
        RegisterCallbacks();
    }

    private void OnDisable()
    {
        UnregisterCallbacks();
    }

    private void OnDestroy()
    {
        UnregisterCallbacks();
        s_SpawnedClients.Clear();
    }

    private void RegisterCallbacks()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;

        if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= SpawnForClient;
            NetworkManager.Singleton.OnClientConnectedCallback += SpawnForClient;

            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void UnregisterCallbacks()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback -= SpawnForClient;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnServerStarted()
    {
        if (NetworkManager.Singleton == null) return;

        s_SpawnedClients.Clear();

        NetworkManager.Singleton.OnClientConnectedCallback -= SpawnForClient;
        NetworkManager.Singleton.OnClientConnectedCallback += SpawnForClient;

        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        s_SpawnedClients.Remove(clientId);
    }

    private void SpawnForClient(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        // 1. Kiểm tra cache static đã spawn cho clientId này chưa
        if (s_SpawnedClients.Contains(clientId))
        {
            Debug.LogWarning($"[RoleBasedSpawner] Client {clientId} đã được spawn trong session này. Bỏ qua để tránh nhân bản!");
            return;
        }

        // 2. Kiểm tra ConnectedClients trong Netcode đã có PlayerObject chưa
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var connectedClient))
        {
            if (connectedClient.PlayerObject != null)
            {
                Debug.LogWarning($"[RoleBasedSpawner] Client {clientId} đã có PlayerObject ({connectedClient.PlayerObject.name}). Bỏ qua!");
                s_SpawnedClients.Add(clientId);
                return;
            }
        }

        s_SpawnedClients.Add(clientId);

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
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;
            if (GameManager.Instance != null)
            {
                spawnPos = GameManager.Instance.GetSpawnPosition(clientId);
                spawnRot = GameManager.Instance.GetSpawnRotation(clientId);
            }

            var go = Instantiate(prefabToSpawn, spawnPos, spawnRot);
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
            Debug.Log($"[RoleBasedSpawner] Đã spawn {(shouldSpawnDad ? "Dad" : "Child")} cho Client {clientId} tại vị trí {spawnPos} thành công.");
        }
        else
        {
            Debug.LogError("[RoleBasedSpawner] Lỗi! Bạn chưa kéo Prefab vào RoleBasedSpawner!");
        }
    }
}
