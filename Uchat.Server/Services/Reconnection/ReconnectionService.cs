using Microsoft.Extensions.Logging;
using Uchat.Server.Services.Redis;

namespace Uchat.Server.Services.Reconnection;

public sealed class ReconnectionService : IReconnectionService
{
    private readonly IRedisService _redisService;
    private readonly ILogger<ReconnectionService> _logger;
    private const string ReconnectionKeyPrefix = "reconnect";
    private const string PreviousConnectionKeyPrefix = "prev_conn";

    public ReconnectionService(IRedisService redisService, ILogger<ReconnectionService> logger)
    {
        _redisService = redisService;
        _logger = logger;
    }

    public async Task<string> TrackConnectionAsync(int userId, string connectionId, string username)
    {
        var trackingKey = GetTrackingKey(userId);
        var sessionId = Guid.NewGuid().ToString();

        // Store current connection info (TTL: 30 minutes)
        await _redisService.SetHashAsync(trackingKey, "connectionId", connectionId, TimeSpan.FromMinutes(30));
        await _redisService.SetHashAsync(trackingKey, "username", username, TimeSpan.FromMinutes(30));
        await _redisService.SetHashAsync(trackingKey, "sessionId", sessionId, TimeSpan.FromMinutes(30));
        await _redisService.SetHashAsync(trackingKey, "connectedAt", DateTime.UtcNow.Ticks.ToString(), TimeSpan.FromMinutes(30));

        _logger.LogInformation("[Reconnection] Tracked connection for user {UserId}: {ConnectionId}", userId, connectionId);

        return sessionId;
    }

    public async Task<string?> GetPreviousConnectionIdAsync(int userId)
    {
        var trackingKey = GetTrackingKey(userId);
        var previousKey = GetPreviousConnectionKey(userId);

        // Try to get from reconnection tracking first
        var connectionData = await _redisService.GetHashAsync(trackingKey, "connectionId");

        if (!string.IsNullOrEmpty(connectionData))
        {
            return connectionData;
        }

        // Fallback to explicit previous connection storage
        return await _redisService.GetStringAsync(previousKey);
    }

    public async Task MarkReconnectedAsync(int userId, string newConnectionId)
    {
        var trackingKey = GetTrackingKey(userId);
        var previousKey = GetPreviousConnectionKey(userId);

        // Get old connection ID before updating
        var oldConnectionId = await _redisService.GetHashAsync(trackingKey, "connectionId");

        if (!string.IsNullOrEmpty(oldConnectionId))
        {
            // Store old connection ID as previous (TTL: 15 minutes)
            await _redisService.SetStringAsync(previousKey, oldConnectionId, TimeSpan.FromMinutes(15));
        }

        // Update with new connection ID
        await _redisService.SetHashAsync(trackingKey, "connectionId", newConnectionId, TimeSpan.FromMinutes(30));
        await _redisService.SetHashAsync(trackingKey, "reconnectedAt", DateTime.UtcNow.Ticks.ToString(), TimeSpan.FromMinutes(30));

        _logger.LogInformation("[Reconnection] User {UserId} reconnected: {OldId} -> {NewId}", userId, oldConnectionId, newConnectionId);
    }

    public async Task CleanupStaleConnectionsAsync(TimeSpan threshold)
    {
        // This would need a scan operation on Redis
        // For now, we rely on TTL expiration which is more efficient
        _logger.LogInformation("[Reconnection] Cleanup scheduled (relies on Redis TTL expiration)");
        await Task.CompletedTask;
    }

    public async Task<bool> HasPendingStateAsync(int userId)
    {
        var trackingKey = GetTrackingKey(userId);
        var connectionData = await _redisService.GetHashAsync(trackingKey, "connectionId");

        return !string.IsNullOrEmpty(connectionData);
    }

    public async Task ClearReconnectionStateAsync(int userId)
    {
        var trackingKey = GetTrackingKey(userId);
        var previousKey = GetPreviousConnectionKey(userId);

        await _redisService.KeyDeleteAsync(trackingKey);
        await _redisService.KeyDeleteAsync(previousKey);

        _logger.LogInformation("[Reconnection] Cleared state for user {UserId}", userId);
    }

    private string GetTrackingKey(int userId) => $"{ReconnectionKeyPrefix}:{userId}";
    private string GetPreviousConnectionKey(int userId) => $"{PreviousConnectionKeyPrefix}:{userId}";
}