using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading;
using System.Threading.Tasks;
using Uchat.Shared;

namespace Uchat.Services;

public class HeartbeatService : IDisposable
{
    private readonly HubConnection _hubConnection;
    private Timer? _heartbeatTimer;
    private const int HeartbeatIntervalSeconds = 30;

    public HeartbeatService(HubConnection hubConnection)
    {
        _hubConnection = hubConnection;
    }

    public void StartHeartbeat()
    {
        if (_heartbeatTimer != null)
        {
            return;
        }

        _heartbeatTimer = new Timer(
            async _ => await SendPingAsync(),
            null,
            TimeSpan.FromSeconds(HeartbeatIntervalSeconds),
            TimeSpan.FromSeconds(HeartbeatIntervalSeconds)
        );

        Logger.Log($"[HEARTBEAT] Service started (interval: {HeartbeatIntervalSeconds}s)");
    }

    public void StopHeartbeat()
    {
        if (_heartbeatTimer != null)
        {
            _heartbeatTimer.Dispose();
            _heartbeatTimer = null;
            Logger.Log("[HEARTBEAT] Service stopped");
        }
    }

    private async Task SendPingAsync()
    {
        try
        {
            if (_hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("Ping").ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"[HEARTBEAT] Ping failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        StopHeartbeat();
    }
}