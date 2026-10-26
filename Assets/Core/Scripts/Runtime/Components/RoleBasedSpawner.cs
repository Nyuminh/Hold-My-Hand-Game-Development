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

    public static RoleBasedSpawner Instance { get; private set; }

    // Theo dõi danh sách clientId đã được spawn để ngăn chặn nhân bản tuyệt đối
    private static readonly HashSet<ulong> s_SpawnedClients = new HashSet<ulong>();
    private bool m_CallbacksRegistered = false;

    private void Awake()
    {
        Instance = this;
        s_SpawnedClients.Clear();
        Debug.Log($"[RoleBasedSpawner] Awake trên GameObject '{gameObject.name}'. Sẵn sàng quản lý spawn nhân vật.");
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

    private void Update()
    {
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsServer || !NetworkManager.Singleton.IsListening) return;

        // Đảm bảo callbacks luôn được đăng ký khi server bắt đầu lắng nghe
        if (!m_CallbacksRegistered)
        {
            RegisterCallbacks();
        }

        // Quét liên tục ConnectedClientsList để spawn ngay lập tức cho bất kỳ client nào chưa có PlayerObject
        // Cơ chế này giải quyết triệt để race condition khi OnServerStarted hoặc OnClientConnectedCallback bị miss trên bản Build Standalone
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        for (int i = 0; i < clients.Count; i++)
        {
            var client = clients[i];
            if (client == null) continue;

            if (client.PlayerObject == null && !s_SpawnedClients.Contains(client.ClientId))
            {
                Debug.Log($"[RoleBasedSpawner] Update phát hiện Client {client.ClientId} chưa có PlayerObject. Đang tiến hành spawn...");
                SpawnForClient(client.ClientId);
            }
        }
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

            m_CallbacksRegistered = true;
        }
    }

    private void UnregisterCallbacks()
    {
        m_CallbacksRegistered = false;
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback -= SpawnForClient;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnServerStarted()
    {
        if (NetworkManager.Singleton == null) return;

        s_SpawnedClients.Clear();
        RegisterCallbacks();

        Debug.Log("[RoleBasedSpawner] OnServerStarted kích hoạt. Quét các client hiện có để spawn...");
        if (NetworkManager.Singleton.ConnectedClientsList != null)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                SpawnForClient(client.ClientId);
            }
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        s_SpawnedClients.Remove(clientId);
        Debug.Log($"[RoleBasedSpawner] Client {clientId} đã ngắt kết nối. Đã xóa khỏi danh sách spawn.");
    }

    public GameObject ResolvePrefab(bool shouldSpawnDad)
    {
        GameObject prefab = shouldSpawnDad ? dadPrefab : childPrefab;
        if (prefab != null) return prefab;

        // Fallback: Tìm trong danh sách NetworkPrefabs của NetworkManager
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig?.Prefabs?.NetworkPrefabsLists != null)
        {
            foreach (var prefabsList in NetworkManager.Singleton.NetworkConfig.Prefabs.NetworkPrefabsLists)
            {
                if (prefabsList == null || prefabsList.PrefabList == null) continue;
                foreach (var item in prefabsList.PrefabList)
                {
                    if (item.Prefab == null) continue;
                    string pName = item.Prefab.name.ToLower();
                    if (shouldSpawnDad && pName.Contains("dad"))
                    {
                        dadPrefab = item.Prefab;
                        return dadPrefab;
                    }
                    if (!shouldSpawnDad && (pName.Contains("child") || pName.Contains("coreplayer")))
                    {
                        childPrefab = item.Prefab;
                        return childPrefab;
                    }
                }
            }
        }

        return null;
    }

    public void SpawnForClient(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        // 1. Kiểm tra cache static đã spawn cho clientId này chưa
        if (s_SpawnedClients.Contains(clientId))
        {
            return;
        }

        // 2. Kiểm tra ConnectedClients trong Netcode đã có PlayerObject chưa
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var connectedClient))
        {
            if (connectedClient.PlayerObject != null)
            {
                Debug.LogWarning($"[RoleBasedSpawner] Client {clientId} đã có PlayerObject ({connectedClient.PlayerObject.name}). Đánh dấu đã spawn!");
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
        
        GameObject prefabToSpawn = ResolvePrefab(shouldSpawnDad);
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
            go.name = shouldSpawnDad ? "Dad" : "The Child";
            CharacterStatsService.ApplyStatsToCharacter(go, shouldSpawnDad);
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
            if (go.TryGetComponent<CoopBoostThrowAbility>(out var boostAbility))
            {
                boostAbility.SetRole(shouldSpawnDad);
            }
            Debug.Log($"[RoleBasedSpawner] ✅ ĐÃ SPAWN {(shouldSpawnDad ? "Dad" : "Child")} cho Client {clientId} tại vị trí {spawnPos} thành công!");
        }
        else
        {
            Debug.LogError($"[RoleBasedSpawner] ❌ Lỗi! Không tìm thấy Prefab cho vai trò {(shouldSpawnDad ? "Dad" : "Child")}!");
        }
    }

    /// <summary>
    /// Xóa danh sách client đã spawn (dùng khi reset scene hoặc khởi động Tutorial)
    /// </summary>
    public static void ClearSpawnedClients()
    {
        s_SpawnedClients.Clear();
    }

    /// <summary>
    /// Hoán đổi vai (Ba <-> Con) giữa các người chơi trong phòng.
    /// </summary>
    public void SwapRolesBetweenPlayers()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        Lobby3DManager.AmIDad = !Lobby3DManager.AmIDad;
        bool hostIsDad = Lobby3DManager.AmIDad;

        var clients = NetworkManager.Singleton.ConnectedClientsList;
        for (int i = 0; i < clients.Count; i++)
        {
            var client = clients[i];
            if (client == null) continue;
            bool isHost = (client.ClientId == NetworkManager.Singleton.LocalClientId);
            bool shouldSpawnDad = (hostIsDad && isHost) || (!hostIsDad && !isHost);
            RespawnForClient(client.ClientId, shouldSpawnDad);
        }

        Debug.Log($"[RoleBasedSpawner] 🔄 Đã hoán đổi vai giữa các người chơi thành công! (Host là {(hostIsDad ? "Ba" : "Con")})");
    }

    /// <summary>
    /// Đổi vai nhân vật ngay lập tức cho client (dùng trong Tutorial hoặc Test)
    /// </summary>
    public void RespawnForClient(ulong clientId, bool asDad)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Lobby3DManager.AmIDad = asDad;
        }
        s_SpawnedClients.Remove(clientId);

        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var connectedClient) && connectedClient.PlayerObject != null)
        {
            spawnPos = connectedClient.PlayerObject.transform.position;
            spawnRot = connectedClient.PlayerObject.transform.rotation;
            var netObj = connectedClient.PlayerObject;
            if (netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
            else
            {
                Destroy(netObj.gameObject);
            }
        }
        else if (GameManager.Instance != null)
        {
            spawnPos = GameManager.Instance.GetSpawnPosition(clientId);
            spawnRot = GameManager.Instance.GetSpawnRotation(clientId);
        }

        s_SpawnedClients.Add(clientId);
        GameObject prefabToSpawn = ResolvePrefab(asDad);
        if (prefabToSpawn != null)
        {
            var go = Instantiate(prefabToSpawn, spawnPos, spawnRot);
            CharacterStatsService.ApplyStatsToCharacter(go, asDad);
            if (go.TryGetComponent<CoopBoostThrowAbility>(out var boostAbility))
            {
                boostAbility.SetRole(asDad);
            }
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
            Debug.Log($"[RoleBasedSpawner] ✅ Respawn thành công vai {(asDad ? "Dad" : "Child")} cho Client {clientId}!");
        }
    }
}
