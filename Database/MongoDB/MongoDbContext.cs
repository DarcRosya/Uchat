using MongoDB.Driver;
using Microsoft.Extensions.Options;

namespace Uchat.Database.MongoDB;

/// <summary>
/// MongoDB context for message storage
/// Provides access to collections and manages indexes
/// </summary>
public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    private readonly MongoDbSettings _settings;
    
    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        _settings = settings.Value;
        
        var client = new MongoClient(_settings.ConnectionString);
        _database = client.GetDatabase(_settings.DatabaseName);
        
        InitializeIndexes();
    }
    
    public MongoDbContext(MongoDbSettings settings)
    {
        _settings = settings;
        
        var client = new MongoClient(_settings.ConnectionString);
        _database = client.GetDatabase(_settings.DatabaseName);
        
        InitializeIndexes();
    }
    
    /// <summary>
    /// Коллекция сообщений
    /// </summary>
    public IMongoCollection<MongoMessage> Messages => 
        _database.GetCollection<MongoMessage>(_settings.MessagesCollectionName);
    
    // ========================================================================
    // ИНИЦИАЛИЗАЦИЯ ИНДЕКСОВ
    // ========================================================================
    // MongoDB РЕКОМЕНДУЕТ создавать индексы вручную
    // 
    // Зачем нужны индексы?
    // - Ускоряют поиск (WHERE chatId = 1)
    // - Ускоряют сортировку (ORDER BY sentAt DESC)
    // - Гарантируют уникальность (UNIQUE INDEX)
    // 
    // БЕЗ индексов MongoDB сканирует ВСЮ коллекцию (медленно!)
    // ========================================================================
    
    /// <summary>
    /// Создать все необходимые индексы для коллекций
    /// 
    /// Вызывается при инициализации контекста
    /// </summary>
    private void InitializeIndexes()
    {
        CreateMessagesIndexes();
    }
    
    /// <summary>
    /// Создать индексы для коллекции messages
    /// 
    /// Индексы:
    /// 1. Composite (ChatId, SentAt DESC) - для cursor-based pagination
    /// 2. ChatId (для поиска сообщений в чате)
    /// 3. SentAt (для сортировки по времени)
    /// 4. Sender.UserId (для поиска сообщений от пользователя)
    /// </summary>
    private void CreateMessagesIndexes()
    {
        var messagesCollection = Messages;
        
        // ====================================================================
        // ИНДЕКС 1: COMPOSITE INDEX (chatId, sentAt DESC)
        // ====================================================================
        // Для cursor-based pagination:
        //   db.messages
        //     .find({ chatId: 1, sentAt: { $lt: lastTimestamp } })
        //     .sort({ sentAt: -1 })
        //     .limit(50)
        // 
        // MongoDB использует ОДИН индекс на весь запрос
        // Сначала chatId (точное совпадение), потом sentAt (сортировка)
        // ====================================================================
        
        var chatIdSentAtIndex = Builders<MongoMessage>.IndexKeys
            .Ascending(m => m.ChatId)
            .Descending(m => m.SentAt);
        
        var chatIdSentAtOptions = new CreateIndexOptions 
        { 
            Name = "idx_chatId_sentAt" 
        };
        
        try
        {
            messagesCollection.Indexes.CreateOne(
                new CreateIndexModel<MongoMessage>(chatIdSentAtIndex, chatIdSentAtOptions));
        }
        catch (MongoCommandException)
        {
            // Индекс уже существует, игнорируем
        }
        
        // ====================================================================
        // ИНДЕКС 2: SINGLE INDEX (chatId)
        // ====================================================================
        // Для поиска всех сообщений в чате (без фильтра по времени)
        // ====================================================================
        
        var chatIdIndex = Builders<MongoMessage>.IndexKeys
            .Ascending(m => m.ChatId);
        
        var chatIdOptions = new CreateIndexOptions 
        { 
            Name = "idx_chatId" 
        };
        
        try
        {
            messagesCollection.Indexes.CreateOne(
                new CreateIndexModel<MongoMessage>(chatIdIndex, chatIdOptions));
        }
        catch (MongoCommandException)
        {
            // Индекс уже существует
        }
        
        // ====================================================================
        // ИНДЕКС 3: SINGLE INDEX (sentAt)
        // ====================================================================
        // Для сортировки по времени (глобальный поиск)
        // ====================================================================
        
        var sentAtIndex = Builders<MongoMessage>.IndexKeys
            .Descending(m => m.SentAt);
        
        var sentAtOptions = new CreateIndexOptions 
        { 
            Name = "idx_sentAt" 
        };
        
        try
        {
            messagesCollection.Indexes.CreateOne(
                new CreateIndexModel<MongoMessage>(sentAtIndex, sentAtOptions));
        }
        catch (MongoCommandException)
        {
            // Индекс уже существует
        }
        
        // ====================================================================
        // ИНДЕКС 4: SINGLE INDEX (sender.userId)
        // ====================================================================
        // Для поиска всех сообщений от пользователя
        // ====================================================================
        
        var senderUserIdIndex = Builders<MongoMessage>.IndexKeys
            .Ascending("sender.userId");
        
        var senderUserIdOptions = new CreateIndexOptions 
        { 
            Name = "idx_sender_userId" 
        };
        
        try
        {
            messagesCollection.Indexes.CreateOne(
                new CreateIndexModel<MongoMessage>(senderUserIdIndex, senderUserIdOptions));
        }
        catch (MongoCommandException)
        {
            // Индекс уже существует
        }
    }
}
