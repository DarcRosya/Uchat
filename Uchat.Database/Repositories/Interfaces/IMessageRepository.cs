using Uchat.Database.MongoDB;

namespace Uchat.Database.Repositories.Interfaces;

/// <summary>
/// Абстракция репозитория сообщений (в проекте сообщения хранятся в MongoDB)
/// Интерфейс работает с `MongoMessage`.
/// </summary>
public interface IMessageRepository
{
    Task<string> CreateAsync(MongoMessage message);
    Task<MongoMessage?> GetByIdAsync(string id);
    Task<List<MongoMessage>> GetDirectMessagesAsync(int userId1, int userId2, int limit = 100);
    Task<List<MongoMessage>> GetChatRoomMessagesAsync(int chatRoomId, int limit = 100);
    Task<bool> EditMessageAsync(string messageId, string newContent);
    Task<bool> DeleteMessageAsync(string messageId);
    Task<bool> MarkAsReadAsync(string messageId, int userId);
    Task<long> GetUnreadCountAsync(int userId);
}
