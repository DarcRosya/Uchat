namespace Uchat.Server.Services.Presence;

public interface IUserPresenceService
{
    Task MarkOnlineAsync(int userId);
    Task RefreshAsync(int userId);
    Task MarkOfflineAsync(int userId);
    Task<bool> IsOnlineAsync(int userId);
}
