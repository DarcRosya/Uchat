namespace Uchat.Server.Services.Reconnection;

/// <summary>
/// Manages reconnection state and session recovery for disconnected users
/// </summary>
public interface IReconnectionService
{
    /// <summary>
    /// Track a user's connection session
    /// </summary>
    Task<string> TrackConnectionAsync(int userId, string connectionId, string username);

    /// <summary>
    /// Get the previous connection ID before disconnect (for recovery purposes)
    /// </summary>
    Task<string?> GetPreviousConnectionIdAsync(int userId);

    /// <summary>
    /// Mark a user as reconnected (successful recovery)
    /// </summary>
    Task MarkReconnectedAsync(int userId, string newConnectionId);

    /// <summary>
    /// Cleanup old connection tracking after extended downtime
    /// </summary>
    Task CleanupStaleConnectionsAsync(TimeSpan threshold);

    /// <summary>
    /// Check if user has pending messages or state to recover
    /// </summary>
    Task<bool> HasPendingStateAsync(int userId);

    /// <summary>
    /// Clear reconnection state for a user
    /// </summary>
    Task ClearReconnectionStateAsync(int userId);
}