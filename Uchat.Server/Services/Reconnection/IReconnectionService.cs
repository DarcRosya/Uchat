namespace Uchat.Server.Services.Reconnection;

public interface IReconnectionService
{
    Task<string> TrackConnectionAsync(int userId, string connectionId, string username);


    Task<string?> GetPreviousConnectionIdAsync(int userId);

    Task MarkReconnectedAsync(int userId, string newConnectionId);

    Task CleanupStaleConnectionsAsync(TimeSpan threshold);
    Task ClearReconnectionStateAsync(int userId);

    Task<bool> HasPendingStateAsync(int userId);
}