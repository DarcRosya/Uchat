using Uchat.Database.MongoDB;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Database.Repositories;

/// <summary>
/// Тонкий адаптер IMessageRepository → IMongoMessageRepository
/// Позволяет инжектить `IMessageRepository` в сервисы, при этом логику хранения делегировать Mongo репозиторию.
/// </summary>
public class MessageRepository : IMessageRepository
{
    private readonly IMongoMessageRepository _mongoRepo;

    public MessageRepository(IMongoMessageRepository mongoRepo)
    {
        _mongoRepo = mongoRepo;
    }

    public async Task<string> CreateAsync(MongoMessage message)
    {
        return await _mongoRepo.SendMessageAsync(message);
    }

    public async Task<MongoMessage?> GetByIdAsync(string id)
    {
        return await _mongoRepo.GetMessageByIdAsync(id);
    }

    public async Task<List<MongoMessage>> GetDirectMessagesAsync(int userId1, int userId2, int limit = 100)
    {
        // В проекте нет единой схемы для direct messages в Mongo; это placeholder-реализация.
        // Лучше уточнить формат хранения direct messages (виртуальный ChatId или отдельная коллекция).
        // Здесь просто возвращаем пустой список, чтобы метод был реализован.
        return new List<MongoMessage>();
    }

    public async Task<List<MongoMessage>> GetChatRoomMessagesAsync(int chatRoomId, int limit = 100)
    {
        return await _mongoRepo.GetChatMessagesAsync(chatRoomId, limit: limit);
    }

    public async Task<bool> EditMessageAsync(string messageId, string newContent)
    {
        return await _mongoRepo.EditMessageAsync(messageId, newContent);
    }

    public async Task<bool> DeleteMessageAsync(string messageId)
    {
        return await _mongoRepo.DeleteMessageAsync(messageId);
    }

    public async Task<bool> MarkAsReadAsync(string messageId, int userId)
    {
        return await _mongoRepo.MarkAsReadAsync(messageId, userId);
    }

    public async Task<long> GetUnreadCountAsync(int userId)
    {
        // Заглушка: подсчёт по всем чатам не реализован в IMongoMessageRepository
        return 0;
    }
}
