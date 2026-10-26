using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Lobbies;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Lưu trữ ngữ cảnh phòng chờ UGS Lobby giữa các scene (Lobby <-> Gameplay)
    /// - Giữ mã phòng và ID phòng để khi thoát game về Lobby, cả 2 người chơi vẫn ở trong phòng cũ.
    /// - Host duy trì Heartbeat định kỳ (15s) ngay cả khi đang trong trận để UGS không xóa phòng.
    /// </summary>
    public static class LobbySessionContext
    {
        public static string CurrentLobbyId { get; set; }
        public static string CurrentLobbyCode { get; set; }
        public static bool IsHost { get; set; }

        private static CancellationTokenSource s_HeartbeatCts;

        public static void SetCurrentLobby(string lobbyId, string lobbyCode, bool isHost)
        {
            CurrentLobbyId = lobbyId;
            CurrentLobbyCode = lobbyCode;
            IsHost = isHost;

            StopHeartbeat();

            if (!string.IsNullOrEmpty(lobbyId))
            {
                s_HeartbeatCts = new CancellationTokenSource();
                if (isHost)
                {
                    _ = HeartbeatRoutine(lobbyId, s_HeartbeatCts.Token);
                }
                else
                {
                    _ = ClientKeepAliveRoutine(lobbyId, s_HeartbeatCts.Token);
                }
            }
        }

        public static void StopHeartbeat()
        {
            if (s_HeartbeatCts != null)
            {
                try
                {
                    s_HeartbeatCts.Cancel();
                    s_HeartbeatCts.Dispose();
                }
                catch { }
                s_HeartbeatCts = null;
            }
        }

        private static async Task HeartbeatRoutine(string lobbyId, CancellationToken ct)
        {
            Debug.Log($"[LobbySessionContext] Bắt đầu Host Heartbeat cho Lobby: {lobbyId}");
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LobbySessionContext] Heartbeat ping warning: {ex.Message}");
                }

                try
                {
                    await Task.Delay(30000, ct); // increased to 30s
                }
                catch
                {
                    break;
                }
            }
            Debug.Log("[LobbySessionContext] Đã dừng Host Heartbeat.");
        }

        private static async Task ClientKeepAliveRoutine(string lobbyId, CancellationToken ct)
        {
            Debug.Log($"[LobbySessionContext] Bắt đầu Client Keep-Alive cho Lobby: {lobbyId}");
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(30000, ct); // increased to 30s
                }
                catch
                {
                    break;
                }

                if (ct.IsCancellationRequested) break;

                try
                {
                    // Non-host players query lobby periodically to refresh player activity (prevents UGS 30s inactivity kick)
                    await LobbyService.Instance.GetLobbyAsync(lobbyId);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LobbySessionContext] Client keep-alive ping warning: {ex.Message}");
                }
            }
            Debug.Log("[LobbySessionContext] Đã dừng Client Keep-Alive.");
        }

        public static void Clear()
        {
            StopHeartbeat();
            CurrentLobbyId = null;
            CurrentLobbyCode = null;
            IsHost = false;
        }
    }
}
