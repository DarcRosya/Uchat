namespace Uchat.Server.Services.Unread;

public interface IUnreadCounterService
{
    Task IncrementUnreadAsync(int chatId, IEnumerable<int> participantIds, int excludeUserId);
    Task<int> GetUnreadCountAsync(int? chatId, int userId);
    Task<Dictionary<int, int>> GetUnreadCountsAsync(int userId, IEnumerable<int> chatIds);
    Task RecalculateUnreadAsync(int chatId, int userId);
}
