/*
 * ============================================================================
 * REPOSITORY IMPLEMENTATION: MongoDB Message Repository
 * ============================================================================
 * 
 * Реализация IMongoMessageRepository
 * 
 * Предоставляет методы для работы с сообщениями в MongoDB
 * 
 * ============================================================================
 */

using MongoDB.Driver;
using MongoDB.Bson;
using Uchat.Database.Context;
using Uchat.Database.MongoDB;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Database.Repositories;

/// <summary>
/// Репозиторий для работы с сообщениями в MongoDB
/// </summary>
public class MongoMessageRepository : IMongoMessageRepository
{
    private readonly MongoDbContext _context;
    private readonly IMongoCollection<MongoMessage> _messages;
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public MongoMessageRepository(MongoDbContext context)
    {
        _context = context;
        _messages = context.Messages;
    }
    
    // ========================================================================
    // СОЗДАНИЕ СООБЩЕНИЙ
    // ========================================================================
    
    public async Task<string> SendMessageAsync(MongoMessage message)
    {
        // Генерируем новый ID если не задан
        if (string.IsNullOrEmpty(message.Id))
        {
            message.Id = ObjectId.GenerateNewId().ToString();
        }
        
        // Устанавливаем время отправки
        message.SentAt = DateTime.UtcNow;
        
        // Вставляем документ в коллекцию
        await _messages.InsertOneAsync(message);
        
        return message.Id;
    }
    
    // ========================================================================
    // ПОЛУЧЕНИЕ СООБЩЕНИЙ
    // ========================================================================
    
    public async Task<List<MongoMessage>> GetChatMessagesAsync(int chatId, int limit = 50, int skip = 0)
    {
        return await _messages
            .Find(m => m.ChatId == chatId && !m.IsDeleted)  // Исключаем удаленные
            .SortByDescending(m => m.SentAt)                 // Сортируем по времени (новые первыми)
            .Skip(skip)                                       // Пропускаем (для пагинации)
            .Limit(limit)                                     // Ограничиваем количество
            .ToListAsync();
    }
    
    public async Task<MongoMessage?> GetMessageByIdAsync(string messageId)
    {
        // Проверяем валидность ObjectId
        if (!ObjectId.TryParse(messageId, out var objectId))
        {
            return null;
        }
        
        return await _messages
            .Find(m => m.Id == messageId)
            .FirstOrDefaultAsync();
    }
    
    public async Task<List<MongoMessage>> GetUnreadMessagesAsync(int chatId, int userId)
    {
        // MongoDB запрос: найти сообщения где userId НЕ в массиве readBy
        var filter = Builders<MongoMessage>.Filter.And(
            Builders<MongoMessage>.Filter.Eq(m => m.ChatId, chatId),
            Builders<MongoMessage>.Filter.Eq(m => m.IsDeleted, false),
            Builders<MongoMessage>.Filter.Not(
                Builders<MongoMessage>.Filter.AnyEq(m => m.ReadBy, userId)
            )
        );
        
        return await _messages
            .Find(filter)
            .SortByDescending(m => m.SentAt)
            .ToListAsync();
    }
    
    public async Task<long> GetUnreadCountAsync(int chatId, int userId)
    {
        var filter = Builders<MongoMessage>.Filter.And(
            Builders<MongoMessage>.Filter.Eq(m => m.ChatId, chatId),
            Builders<MongoMessage>.Filter.Eq(m => m.IsDeleted, false),
            Builders<MongoMessage>.Filter.Not(
                Builders<MongoMessage>.Filter.AnyEq(m => m.ReadBy, userId)
            )
        );
        
        return await _messages.CountDocumentsAsync(filter);
    }
    
    // ========================================================================
    // РЕДАКТИРОВАНИЕ И УДАЛЕНИЕ
    // ========================================================================
    
    public async Task<bool> EditMessageAsync(string messageId, string newContent)
    {
        // Проверяем валидность ObjectId
        if (!ObjectId.TryParse(messageId, out var objectId))
        {
            return false;
        }
        
        // Фильтр: найти по ID
        var filter = Builders<MongoMessage>.Filter.Eq(m => m.Id, messageId);
        
        // Обновление: установить новый content и editedAt
        var update = Builders<MongoMessage>.Update
            .Set(m => m.Content, newContent)
            .Set(m => m.EditedAt, DateTime.UtcNow);
        
        var result = await _messages.UpdateOneAsync(filter, update);
        
        return result.ModifiedCount > 0;
    }
    
    public async Task<bool> DeleteMessageAsync(string messageId)
    {
        // Проверяем валидность ObjectId
        if (!ObjectId.TryParse(messageId, out var objectId))
        {
            return false;
        }
        
        // SOFT DELETE: устанавливаем isDeleted = true
        // Физическое удаление произойдет через 30 дней (TTL Index)
        var filter = Builders<MongoMessage>.Filter.Eq(m => m.Id, messageId);
        var update = Builders<MongoMessage>.Update.Set(m => m.IsDeleted, true);
        
        var result = await _messages.UpdateOneAsync(filter, update);
        
        return result.ModifiedCount > 0;
    }
    
    // ========================================================================
    // РЕАКЦИИ
    // ========================================================================
    
    public async Task<bool> AddReactionAsync(string messageId, string emoji, int userId)
    {
        // Проверяем валидность ObjectId
        if (!ObjectId.TryParse(messageId, out var objectId))
        {
            return false;
        }
        
        // Фильтр: найти сообщение по ID
        var filter = Builders<MongoMessage>.Filter.Eq(m => m.Id, messageId);
        
        // Обновление: добавить userId в массив reactions[emoji]
        // $addToSet - добавляет только если элемента еще нет (предотвращает дубликаты)
        var update = Builders<MongoMessage>.Update
            .AddToSet($"reactions.{emoji}", userId);
        
        var result = await _messages.UpdateOneAsync(filter, update);
        
        return result.ModifiedCount > 0;
    }
    
    public async Task<bool> RemoveReactionAsync(string messageId, string emoji, int userId)
    {
        // Проверяем валидность ObjectId
        if (!ObjectId.TryParse(messageId, out var objectId))
        {
            return false;
        }
        
        // Фильтр: найти сообщение по ID
        var filter = Builders<MongoMessage>.Filter.Eq(m => m.Id, messageId);
        
        // Обновление: удалить userId из массива reactions[emoji]
        // $pull - удаляет элемент из массива
        var update = Builders<MongoMessage>.Update
            .Pull($"reactions.{emoji}", userId);
        
        var result = await _messages.UpdateOneAsync(filter, update);
        
        return result.ModifiedCount > 0;
    }
    
    // ========================================================================
    // СТАТУС ПРОЧТЕНИЯ
    // ========================================================================
    
    public async Task<bool> MarkAsReadAsync(string messageId, int userId)
    {
        // Проверяем валидность ObjectId
        if (!ObjectId.TryParse(messageId, out var objectId))
        {
            return false;
        }
        
        // Фильтр: найти сообщение по ID
        var filter = Builders<MongoMessage>.Filter.Eq(m => m.Id, messageId);
        
        // Обновление: добавить userId в массив readBy
        // $addToSet - добавляет только если userId еще нет в массиве
        var update = Builders<MongoMessage>.Update
            .AddToSet(m => m.ReadBy, userId);
        
        var result = await _messages.UpdateOneAsync(filter, update);
        
        return result.ModifiedCount > 0;
    }
    
    public async Task<long> MarkAllAsReadAsync(int chatId, int userId)
    {
        // Фильтр: сообщения в чате, которые пользователь еще не прочитал
        var filter = Builders<MongoMessage>.Filter.And(
            Builders<MongoMessage>.Filter.Eq(m => m.ChatId, chatId),
            Builders<MongoMessage>.Filter.Eq(m => m.IsDeleted, false),
            Builders<MongoMessage>.Filter.Not(
                Builders<MongoMessage>.Filter.AnyEq(m => m.ReadBy, userId)
            )
        );
        
        // Обновление: добавить userId в readBy для ВСЕХ найденных сообщений
        var update = Builders<MongoMessage>.Update
            .AddToSet(m => m.ReadBy, userId);
        
        var result = await _messages.UpdateManyAsync(filter, update);
        
        return result.ModifiedCount;
    }
    
    // ========================================================================
    // ПОИСК
    // ========================================================================
    
    public async Task<List<MongoMessage>> SearchMessagesAsync(int chatId, string searchQuery, int limit = 20)
    {
        // Фильтр: поиск по тексту (case-insensitive)
        var filter = Builders<MongoMessage>.Filter.And(
            Builders<MongoMessage>.Filter.Eq(m => m.ChatId, chatId),
            Builders<MongoMessage>.Filter.Eq(m => m.IsDeleted, false),
            Builders<MongoMessage>.Filter.Regex(m => m.Content, new BsonRegularExpression(searchQuery, "i"))
        );
        
        return await _messages
            .Find(filter)
            .SortByDescending(m => m.SentAt)
            .Limit(limit)
            .ToListAsync();
    }
}

/*
 * ============================================================================
 * ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ
 * ============================================================================
 * 
 * 1. ОТПРАВКА СООБЩЕНИЯ:
 * 
 *    var repo = new MongoMessageRepository(mongoContext);
 *    
 *    var message = new MongoMessage
 *    {
 *        ChatId = 1,
 *        Sender = new MessageSender
 *        {
 *            UserId = 100,
 *            Username = "alice",
 *            DisplayName = "Alice Smith",
 *            AvatarUrl = "/alice.jpg"
 *        },
 *        Content = "Hello everyone!",
 *        Type = "text"
 *    };
 *    
 *    var messageId = await repo.SendMessageAsync(message);
 *    Console.WriteLine($"Сообщение отправлено: {messageId}");
 * 
 * 
 * 2. ПОЛУЧЕНИЕ ИСТОРИИ ЧАТА:
 * 
 *    // Первые 50 сообщений
 *    var messages = await repo.GetChatMessagesAsync(chatId: 1, limit: 50);
 *    
 *    // Следующие 50 (для "загрузить еще")
 *    var moreMessages = await repo.GetChatMessagesAsync(chatId: 1, limit: 50, skip: 50);
 * 
 * 
 * 3. ДОБАВЛЕНИЕ РЕАКЦИИ:
 * 
 *    await repo.AddReactionAsync(messageId, "👍", userId: 100);
 *    await repo.AddReactionAsync(messageId, "❤️", userId: 200);
 * 
 * 
 * 4. ПОМЕТИТЬ КАК ПРОЧИТАННОЕ:
 * 
 *    // Одно сообщение
 *    await repo.MarkAsReadAsync(messageId, userId: 100);
 *    
 *    // Все сообщения в чате
 *    var count = await repo.MarkAllAsReadAsync(chatId: 1, userId: 100);
 *    Console.WriteLine($"Помечено {count} сообщений");
 * 
 * 
 * 5. РЕДАКТИРОВАНИЕ:
 * 
 *    await repo.EditMessageAsync(messageId, "Updated message text");
 * 
 * 
 * 6. УДАЛЕНИЕ (SOFT DELETE):
 * 
 *    await repo.DeleteMessageAsync(messageId);
 *    // Сообщение скрыто, но физически удалится через 30 дней (TTL Index)
 * 
 * 
 * 7. ПОИСК:
 * 
 *    var results = await repo.SearchMessagesAsync(chatId: 1, "hello");
 *    Console.WriteLine($"Найдено {results.Count} сообщений");
 * 
 * 
 * 8. ПОЛУЧЕНИЕ НЕПРОЧИТАННЫХ:
 * 
 *    var unread = await repo.GetUnreadMessagesAsync(chatId: 1, userId: 100);
 *    var unreadCount = await repo.GetUnreadCountAsync(chatId: 1, userId: 100);
 *    
 *    Console.WriteLine($"Непрочитанных: {unreadCount}");
 * 
 * ============================================================================
 */
