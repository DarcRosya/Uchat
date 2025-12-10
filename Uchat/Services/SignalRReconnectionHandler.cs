using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;
using Avalonia.Media;
using Uchat.Shared;

namespace Uchat.Services;

/// <summary>
/// Handles automatic reconnection logic for SignalR hub connection
/// </summary>
public class SignalRReconnectionHandler
{
    private readonly HubConnection _hubConnection;
    private Action<string, IBrush>? _statusCallback;
    private DateTime? _lastDisconnectTime;
    private int _reconnectAttempts;
    private const int MaxReconnectAttempts = 10;

    public SignalRReconnectionHandler(HubConnection hubConnection, Action<string, IBrush>? statusCallback = null)
    {
        _hubConnection = hubConnection;
        _statusCallback = statusCallback;
        _reconnectAttempts = 0;
    }

    /// <summary>
    /// Setup reconnection event handlers
    /// </summary>
    public void SetupReconnectionHandlers()
    {
        _hubConnection.Reconnecting += OnReconnecting;
        _hubConnection.Reconnected += OnReconnected;
        _hubConnection.Closed += OnClosed;
    }

    private async Task OnReconnecting(Exception? exception)
    {
        _reconnectAttempts++;
        _lastDisconnectTime = DateTime.UtcNow;

        var message = $"Reconnecting... (attempt {_reconnectAttempts}/{MaxReconnectAttempts})";
        
        Logger.Log($"[RECONNECTION] {message}");
        
        UpdateStatus(message, Brushes.Orange);

        if (exception != null)
        {
            Logger.Log($"[RECONNECTION] Disconnect reason: {exception.Message}");
        }

        await Task.CompletedTask;
    }

    private async Task OnReconnected(string? connectionId)
    {
        _reconnectAttempts = 0;

        Logger.Log($"[RECONNECTION] Successfully reconnected with connectionId: {connectionId}");
        Logger.Log($"[RECONNECTION] Reconnection time: {(DateTime.UtcNow - _lastDisconnectTime)?.TotalSeconds:F2}s");

        UpdateStatus("? Reconnected", Brushes.Green);

        // Notify that reconnection was successful
        await _hubConnection.InvokeAsync("Heartbeat").ConfigureAwait(false);

        await Task.CompletedTask;
    }

    private Task OnClosed(Exception? exception)
    {
        if (_reconnectAttempts >= MaxReconnectAttempts)
        {
            Logger.Log("[RECONNECTION] Max reconnection attempts reached. Connection permanently closed.");
            UpdateStatus("? Connection lost (max retries)", Brushes.Red);
        }
        else if (exception != null)
        {
            Logger.Log($"[RECONNECTION] Connection closed: {exception.Message}");
            UpdateStatus("? Connection closed", Brushes.Red);
        }
        else
        {
            Logger.Log("[RECONNECTION] Connection closed by server");
            UpdateStatus("? Disconnected", Brushes.Gray);
        }

        return Task.CompletedTask;
    }

    private void UpdateStatus(string text, IBrush color)
    {
        _statusCallback?.Invoke(text, color);
    }

    /// <summary>
    /// Reset reconnection state (call after successful login)
    /// </summary>
    public void ResetReconnectionState()
    {
        _reconnectAttempts = 0;
        _lastDisconnectTime = null;
        Logger.Log("[RECONNECTION] State reset");
    }
}