using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Uchat.Database.MongoDB;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Database.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly MongoDbContext _context;
    private readonly IMongoCollection<MongoMessage> _messages;
    
    public MessageRepository(MongoDbContext context)
    {
        _context = context;
        _messages = context.Messages;
    }
    public async Task<List<MongoMessage>> GetChatMessagesAsync(int chatId, int limit = 50, DateTime? lastTimestamp = null)
    {
        FilterDefinition<MongoMessage> filter;
        
        if (lastTimestamp == null)
        {
            // ПЕРВАЯ ЗАГРУЗКА: последние N сообщений
            filter = Builders<MongoMessage>.Filter.And(
                Builders<MongoMessage>.Filter.Eq(m => m.ChatId, chatId),
                Builders<MongoMessage>.Filter.Eq(m => m.IsDeleted, false)
            );
        }
        else
        {
            // ЗАГРУЗИТЬ ЕЩЕ: старые сообщения до lastTimestamp
            filter = Builders<MongoMessage>.Filter.And(
                Builders<MongoMessage>.Filter.Eq(m => m.ChatId, chatId),
                Builders<MongoMessage>.Filter.Eq(m => m.IsDeleted, false),
                Builders<MongoMessage>.Filter.Lt(m => m.SentAt, lastTimestamp.Value)
            );
        }
        
        var result = await _messages
            .Find(filter)
            .SortByDescending(m => m.SentAt)
            .Limit(limit)
            .ToListAsync();
            
        return result;
    }
    
    public async Task<MongoMessage?> GetMessageByIdAsync(string messageId)
    {
        var filter = Builders<MongoMessage>.Filter.Eq(m => m.Id, messageId);
        var result = await _messages.Find(filter).FirstOrDefaultAsync();
        return result;
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
        
        var result = await _messages
            .Find(filter)
            .SortByDescending(m => m.SentAt)
            .ToListAsync();
            
        return result;
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
        
        var count = await _messages.CountDocumentsAsync(filter);
        return count;
    }
    
    // ========================================================================
    // РЕДАКТИРОВАНИЕ И УДАЛЕНИЕ
    // ========================================================================
    
    public async Task<bool> EditMessageAsync(string messageId, string newContent)
    {
        var filter = Builders<MongoMessage>.Filter.Eq(m => m.Id, messageId);
        var update = Builders<MongoMessage>.Update
            .Set(m => m.Content, newContent)
            .Set(m => m.EditedAt, DateTime.UtcNow);

        var result = await _messages.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }
    
    public async Task<bool> DeleteMessageAsync(string messageId)
    {
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
        var filter = Builders<MongoMessage>.Filter.Eq(m => m.Id, messageId);
        
        // Используем $addToSet для добавления userId в массив reactions[emoji]
        var update = Builders<MongoMessage>.Update.AddToSet($"reactions.{emoji}", userId);

        var result = await _messages.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }
    
    public async Task<bool> RemoveReactionAsync(string messageId, string emoji, int userId)
    {
        var filter = Builders<MongoMessage>.Filter.Eq(m => m.Id, messageId);
        
        // Используем $pull для удаления userId из массива reactions[emoji]
        var update = Builders<MongoMessage>.Update.Pull($"reactions.{emoji}", userId);

        var result = await _messages.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }
    
    // ========================================================================
    // СТАТУС ПРОЧТЕНИЯ
    // ========================================================================
    
    public async Task<bool> MarkAsReadAsync(string messageId, int userId)
    {
        var filter = Builders<MongoMessage>.Filter.Eq(m => m.Id, messageId);
        var update = Builders<MongoMessage>.Update.AddToSet(m => m.ReadBy, userId);

        var result = await _messages.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }
    
    public async Task<long> MarkAllAsReadAsync(int chatId, int userId)
    {
        var filter = Builders<MongoMessage>.Filter.And(
            Builders<MongoMessage>.Filter.Eq(m => m.ChatId, chatId),
            Builders<MongoMessage>.Filter.Eq(m => m.IsDeleted, false),
            Builders<MongoMessage>.Filter.Not(
                Builders<MongoMessage>.Filter.AnyEq(m => m.ReadBy, userId)
            )
        );
        
        var update = Builders<MongoMessage>.Update.AddToSet(m => m.ReadBy, userId);
        var result = await _messages.UpdateManyAsync(filter, update);
        
        return result.ModifiedCount;
    }
    
    public async Task<long> MarkAsReadUntilAsync(int chatId, int userId, DateTime untilTimestamp)
    {
        var filter = Builders<MongoMessage>.Filter.And(
            Builders<MongoMessage>.Filter.Eq(m => m.ChatId, chatId),
            Builders<MongoMessage>.Filter.Eq(m => m.IsDeleted, false),
            Builders<MongoMessage>.Filter.Lte(m => m.SentAt, untilTimestamp),
            Builders<MongoMessage>.Filter.Not(
                Builders<MongoMessage>.Filter.AnyEq(m => m.ReadBy, userId)
            )
        );
        
        var update = Builders<MongoMessage>.Update.AddToSet(m => m.ReadBy, userId);
        var result = await _messages.UpdateManyAsync(filter, update);
        
        return result.ModifiedCount;
    }
    
    // ========================================================================
    // ПОИСК
    // ========================================================================
    
    public async Task<List<MongoMessage>> SearchMessagesAsync(int chatId, string searchQuery, int limit = 20)
    {
        // Фильтр: поиск по тексту (case-insensitive через regex)
        var filter = Builders<MongoMessage>.Filter.And(
            Builders<MongoMessage>.Filter.Eq(m => m.ChatId, chatId),
            Builders<MongoMessage>.Filter.Eq(m => m.IsDeleted, false),
            Builders<MongoMessage>.Filter.Regex(m => m.Content, new BsonRegularExpression(searchQuery, "i"))
        );
        
        var result = await _messages
            .Find(filter)
            .SortByDescending(m => m.SentAt)
            .Limit(limit)
            .ToListAsync();
            
        return result;
    }
}
