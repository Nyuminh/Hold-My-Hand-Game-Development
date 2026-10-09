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
        
        await Task.Delay(500); // Give the game scene a moment to fully initialize

        if (Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("[AutoSessionStarter] NetworkManager was already listening! Shutting down...");
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
            await Task.Delay(1000);
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
            
            Debug.Log($"[AutoSessionStarter] Đã tạo Session: {session.Id}. Đang báo cho Client...");
            var updateOptions = new UpdateLobbyOptions {
                Data = new Dictionary<string, DataObject> {
                    { "sessionId", new DataObject(DataObject.VisibilityOptions.Public, session.Id) }
                }
            };
            await LobbyService.Instance.UpdateLobbyAsync(m_LobbyId, updateOptions);
            
            // Host có thể rời sảnh matchmaking cũ
            try { await LobbyService.Instance.RemovePlayerAsync(m_LobbyId, Unity.Services.Authentication.AuthenticationService.Instance.PlayerId); } catch { }
            
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
            string sessionId = null;
            
            // Liên tục kiểm tra sảnh matchmaking xem Host đã đẩy sessionId lên chưa
            while (string.IsNullOrEmpty(sessionId) && this != null)
            {
                var lobby = await LobbyService.Instance.GetLobbyAsync(m_LobbyId);
                if (lobby.Data.ContainsKey("sessionId") && !string.IsNullOrEmpty(lobby.Data["sessionId"].Value))
                {
                    sessionId = lobby.Data["sessionId"].Value;
                    break;
                }
                await Task.Delay(1000);
            }

            if (!string.IsNullOrEmpty(sessionId))
            {
                Debug.Log($"[AutoSessionStarter] Tìm thấy Session: {sessionId}. Bắt đầu Join...");
                
                // Rời sảnh matchmaking cũ để tránh kẹt
                try { await LobbyService.Instance.RemovePlayerAsync(m_LobbyId, Unity.Services.Authentication.AuthenticationService.Instance.PlayerId); } catch { }

                var joinOptions = new JoinSessionOptions { Type = "default-session" };
                await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId, joinOptions);
                Debug.Log("[AutoSessionStarter] Client hoàn tất tự động vào game!");
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
