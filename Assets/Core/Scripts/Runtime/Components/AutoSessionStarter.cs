using UnityEngine;
using Unity.Services.Multiplayer;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

public class AutoSessionStarter : MonoBehaviour
{
    private bool m_IsHost;
    private string m_LobbyId;
    private bool m_HasStarted = false;

    public void Setup(bool isHost, string lobbyId)
    {
        m_IsHost = isHost;
        m_LobbyId = lobbyId;
    }

    private async void Update()
    {
        if (m_HasStarted) return;
        
        // Đợi cho đến khi sang Game Scene và MultiplayerService/NetworkManager đã sẵn sàng
        if (MultiplayerService.Instance == null) return;
        if (Unity.Netcode.NetworkManager.Singleton == null) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Lobby")) return;

        m_HasStarted = true;
        
        // Đợi lâu hơn để cho SDK Lobby hoàn tất cleanup nội bộ trước khi bắt đầu Session mới
        await Task.Delay(1500);

        if (Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("[AutoSessionStarter] NetworkManager was already listening! Shutting down...");
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
            await Task.Delay(1500);
        }

        if (m_IsHost)
        {
            await StartHostRoutine();
        }
        else
        {
            await StartClientRoutine();
        }
    }

    private async Task StartHostRoutine()
    {
        try 
        {
            Debug.Log("[AutoSessionStarter] Host đang tạo Multiplayer Session...");
            var options = new SessionOptions { 
                MaxPlayers = 2, 
                Name = $"Game_{Random.Range(1000,9999)}",
                Type = "default-session"
            }.WithRelayNetwork();
            var session = await MultiplayerService.Instance.CreateSessionAsync(options);
            
            // Đảm bảo RoleBasedSpawner luôn tồn tại trên NetworkManager
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.GetComponent<RoleBasedSpawner>() == null)
            {
                Debug.Log("[AutoSessionStarter] RoleBasedSpawner chưa có trên NetworkManager, đang thêm tự động...");
                Unity.Netcode.NetworkManager.Singleton.gameObject.AddComponent<RoleBasedSpawner>();
            }

            Debug.Log($"[AutoSessionStarter] Đã tạo Session: {session.Id}. Đang báo cho Client...");
            var updateOptions = new UpdateLobbyOptions {
                Data = new Dictionary<string, DataObject> {
                    { "sessionId", new DataObject(DataObject.VisibilityOptions.Public, session.Id) },
                    { "status", new DataObject(DataObject.VisibilityOptions.Public, "InGame") }
                }
            };
            await LobbyService.Instance.UpdateLobbyAsync(m_LobbyId, updateOptions);
            
            Debug.Log("[AutoSessionStarter] Host hoàn tất tự động vào game!");
            Destroy(gameObject);
        }
        catch (System.Exception e) 
        { 
            Debug.LogError($"[AutoSessionStarter] Host Error: {e}"); 
            Destroy(gameObject);
        }
    }

    private async Task StartClientRoutine()
    {
        try 
        {
            Debug.Log("[AutoSessionStarter] Client đang chờ Host tạo Session...");
            
            // Dừng keep-alive trước khi poll để tránh 429
            Blocks.Gameplay.Core.LobbySessionContext.StopHeartbeat();
            
            string sessionId = null;
            
            // Liên tục kiểm tra sảnh matchmaking xem Host đã đẩy sessionId lên chưa
            int pollAttempts = 0;
            while (string.IsNullOrEmpty(sessionId) && this != null && pollAttempts < 30)
            {
                pollAttempts++;
                try
                {
                    var lobby = await LobbyService.Instance.GetLobbyAsync(m_LobbyId);
                    if (lobby.Data.ContainsKey("sessionId") && !string.IsNullOrEmpty(lobby.Data["sessionId"].Value))
                    {
                        sessionId = lobby.Data["sessionId"].Value;
                        break;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[AutoSessionStarter] Poll Lobby attempt {pollAttempts} failed: {ex.Message}");
                }
                await Task.Delay(5000);
            }

            if (!string.IsNullOrEmpty(sessionId))
            {
                Debug.Log($"[AutoSessionStarter] Tìm thấy Session: {sessionId}. Chờ SDK cleanup rồi Join...");
                
                // Đợi thêm để SDK nội bộ hoàn tất cleanup Lobby session cũ
                await Task.Delay(2000);

                var joinOptions = new JoinSessionOptions { Type = "default-session" };
                
                // Thử join với retry
                int maxRetries = 3;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        Debug.Log($"[AutoSessionStarter] Join attempt {attempt}/{maxRetries}...");
                        await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId, joinOptions);
                        Debug.Log("[AutoSessionStarter] Client hoàn tất tự động vào game!");
                        
                        // Giữ player trong lobby để khi quay về vẫn cùng phòng
                        break;
                    }
                    catch (System.Exception ex) when (attempt < maxRetries)
                    {
                        Debug.LogWarning($"[AutoSessionStarter] Join attempt {attempt} failed: {ex.Message}. Retrying in 4s...");
                        await Task.Delay(4000);
                    }
                }
            }
            else
            {
                Debug.LogError("[AutoSessionStarter] Không tìm thấy sessionId sau 30 lần thử!");
            }
            Destroy(gameObject);
        }
        catch (System.Exception e) 
        { 
            Debug.LogError($"[AutoSessionStarter] Client Error: {e}"); 
            Destroy(gameObject);
        }
    }
}
