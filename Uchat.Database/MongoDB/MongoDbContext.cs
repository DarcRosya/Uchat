/*
 * ============================================================================
 * MONGODB CONTEXT (Контекст базы данных MongoDB)
 * ============================================================================
 * 
 * ЧТО ТАКОЕ MongoDbContext?
 * 
 * Это аналог UchatDbContext (для SQLite), но для MongoDB:
 * 1. Подключение к MongoDB серверу
 * 2. Доступ к коллекциям (аналог DbSet<T>)
 * 3. Настройка индексов
 * 4. Создание TTL индексов для автоудаления
 * 
 * ============================================================================
 * ОТЛИЧИЕ ОТ EF CORE DbContext
 * ============================================================================
 * 
 * EF Core DbContext (SQLite):
 *   - DbSet<User> Users
 *   - OnModelCreating() для конфигурации
 *   - SaveChanges() для применения изменений
 *   - Миграции для изменения схемы
 * 
 * MongoDbContext:
 *   - IMongoCollection<MongoMessage> Messages
 *   - CreateIndexes() для индексов
 *   - Нет SaveChanges() (сохраняется сразу)
 *   - Нет миграций (schema-less)
 * 
 * ============================================================================
 * ПОДКЛЮЧЕНИЕ К MONGODB
 * ============================================================================
 * 
 * Connection String формат:
 *   "mongodb://localhost:27017"  - локальный MongoDB
 *   "mongodb://username:password@host:27017/dbname"  - с аутентификацией
 *   "mongodb+srv://cluster.mongodb.net/dbname"  - MongoDB Atlas (cloud)
 * 
 * ============================================================================
 */

using MongoDB.Driver;
using MongoDB.Bson;
using Uchat.Database.MongoDB;

namespace Uchat.Database.Context;

/// <summary>
/// Контекст для работы с MongoDB
/// 
/// Предоставляет доступ к коллекциям:
/// - Messages (сообщения в чатах)
/// 
/// Автоматически создает индексы при инициализации
/// </summary>
public class MongoDbContext
{
    // ========================================================================
    // ПОЛЯ
    // ========================================================================
    
    /// <summary>
    /// Клиент подключения к MongoDB серверу
    /// Singleton - создается один раз и переиспользуется
    /// </summary>
    private readonly IMongoClient _client;
    
    /// <summary>
    /// База данных MongoDB
    /// Аналог database в SQL (содержит коллекции)
    /// </summary>
    private readonly IMongoDatabase _database;
    
    // ========================================================================
    // КОНСТРУКТОР
    // ========================================================================
    
    /// <summary>
    /// Инициализация MongoDB контекста
    /// 
    /// Параметры:
    /// - connectionString: строка подключения к MongoDB
    ///   Примеры:
    ///     "mongodb://localhost:27017"
    ///     "mongodb://user:pass@localhost:27017"
    ///     "mongodb+srv://cluster.mongodb.net"
    /// 
    /// - databaseName: имя базы данных
    ///   Пример: "uchat" или "uchat_production"
    /// 
    /// Использование:
    ///   var context = new MongoDbContext(
    ///       "mongodb://localhost:27017",
    ///       "uchat"
    ///   );
    /// </summary>
    public MongoDbContext(string connectionString, string databaseName)
    {
        // 1. Создаем клиента MongoDB (singleton)
        _client = new MongoClient(connectionString);
        
        // 2. Получаем ссылку на базу данных
        //    Если БД не существует - создастся автоматически при первой вставке
        _database = _client.GetDatabase(databaseName);
        
        // 3. Создаем индексы для коллекций
        //    Это нужно сделать ОДИН РАЗ при первом запуске
        //    Повторный вызов безопасен (индексы не дублируются)
        InitializeIndexes();
    }
    
    // ========================================================================
    // КОЛЛЕКЦИИ (аналог DbSet<T>)
    // ========================================================================
    // В MongoDB данные хранятся в КОЛЛЕКЦИЯХ
    // Коллекция = аналог таблицы в SQL
    // 
    // Но в отличие от SQL:
    // - Коллекция создается автоматически при первой вставке
    // - Нет строгой схемы (schema-less)
    // - Каждый документ может иметь разные поля
    // ========================================================================
    
    /// <summary>
    /// Коллекция сообщений
    /// В MongoDB: "messages"
    /// 
    /// Использование:
    ///   await context.Messages.InsertOneAsync(message);
    ///   var messages = await context.Messages.Find(m => m.ChatId == 1).ToListAsync();
    /// 
    /// Аналог SQL:
    ///   INSERT INTO messages ...
    ///   SELECT * FROM messages WHERE chatId = 1
    /// </summary>
    public IMongoCollection<MongoMessage> Messages => 
        _database.GetCollection<MongoMessage>("messages");
    
    // ========================================================================
    // ИНИЦИАЛИЗАЦИЯ ИНДЕКСОВ
    // ========================================================================
    // MongoDB РЕКОМЕНДУЕТ создавать индексы вручную
    // 
    // Зачем нужны индексы?
    // - Ускоряют поиск (WHERE chatId = 1)
    // - Ускоряют сортировку (ORDER BY sentAt DESC)
    // - Гарантируют уникальность (UNIQUE INDEX)
    // - Автоматически удаляют старые документы (TTL INDEX)
    // 
    // БЕЗ индексов MongoDB сканирует ВСЮ коллекцию (медленно!)
    // ========================================================================
    
    /// <summary>
    /// Создать все необходимые индексы для коллекций
    /// 
    /// Вызывается ОДИН РАЗ при инициализации контекста
    /// Повторный вызов безопасен (не создает дубликаты)
    /// </summary>
    private void InitializeIndexes()
    {
        CreateMessagesIndexes();
    }
    
    /// <summary>
    /// Создать индексы для коллекции messages
    /// 
    /// Индексы:
    /// 1. chatId (для поиска сообщений в чате)
    /// 2. sentAt (для сортировки по времени)
    /// 3. chatId + sentAt (составной, для оптимизации запросов)
    /// 4. TTL Index на sentAt (для автоудаления старых сообщений)
    /// </summary>
    private void CreateMessagesIndexes()
    {
        var messagesCollection = Messages;
        
        // ====================================================================
        // INDEX 1: chatId (для быстрого поиска сообщений в конкретном чате)
        // ====================================================================
        // SQL аналог: CREATE INDEX IX_Messages_ChatId ON messages(chatId)
        // 
        // Запрос:
        //   db.messages.find({ chatId: 1 })
        // 
        // Без индекса: сканирует ВСЮ коллекцию (медленно!)
        // С индексом: мгновенный поиск через B-tree
        // ====================================================================
        
        var chatIdIndex = Builders<MongoMessage>.IndexKeys
            .Ascending(m => m.ChatId);
        
        messagesCollection.Indexes.CreateOne(
            new CreateIndexModel<MongoMessage>(
                chatIdIndex,
                new CreateIndexOptions 
                { 
                    Name = "IX_Messages_ChatId",
                    Background = true  // Создавать в фоне (не блокирует запросы)
                }
            )
        );
        
        // ====================================================================
        // INDEX 2: sentAt (для сортировки по времени)
        // ====================================================================
        // SQL аналог: CREATE INDEX IX_Messages_SentAt ON messages(sentAt)
        // 
        // Запрос:
        //   db.messages.find().sort({ sentAt: -1 })
        // 
        // Используется для:
        // - Сортировка "сначала новые" (ORDER BY sentAt DESC)
        // - Поиск сообщений за период (WHERE sentAt > date)
        // ====================================================================
        
        var sentAtIndex = Builders<MongoMessage>.IndexKeys
            .Descending(m => m.SentAt);  // -1 = сортировка по убыванию
        
        messagesCollection.Indexes.CreateOne(
            new CreateIndexModel<MongoMessage>(
                sentAtIndex,
                new CreateIndexOptions 
                { 
                    Name = "IX_Messages_SentAt",
                    Background = true
                }
            )
        );
        
        // ====================================================================
        // INDEX 3: chatId + sentAt (СОСТАВНОЙ ИНДЕКС - САМЫЙ ВАЖНЫЙ!)
        // ====================================================================
        // SQL аналог: CREATE INDEX IX_Messages_Chat_Time ON messages(chatId, sentAt DESC)
        // 
        // Запрос:
        //   db.messages
        //     .find({ chatId: 1 })
        //     .sort({ sentAt: -1 })
        //     .limit(50)
        // 
        // Это САМЫЙ ЧАСТЫЙ запрос в мессенджере:
        // "Загрузить последние 50 сообщений из чата #1"
        // 
        // Составной индекс ускоряет И фильтрацию И сортировку одновременно!
        // ====================================================================
        
        var chatTimestampIndex = Builders<MongoMessage>.IndexKeys
            .Ascending(m => m.ChatId)      // Сначала фильтруем по chatId
            .Descending(m => m.SentAt);    // Потом сортируем по времени (новые первыми)
        
        messagesCollection.Indexes.CreateOne(
            new CreateIndexModel<MongoMessage>(
                chatTimestampIndex,
                new CreateIndexOptions 
                { 
                    Name = "IX_Messages_Chat_Timestamp",
                    Background = true
                }
            )
        );
        
        // ====================================================================
        // INDEX 4: TTL INDEX (автоудаление старых сообщений)
        // ====================================================================
        // SQL НЕ ПОДДЕРЖИВАЕТ автоудаление!
        // В SQL нужен CRON для очистки:
        //   DELETE FROM messages WHERE sentAt < NOW() - INTERVAL 30 DAYS;
        // 
        // MongoDB TTL Index делает это АВТОМАТИЧЕСКИ:
        // - Каждые 60 секунд фоновый процесс проверяет sentAt
        // - Если (sentAt + 30 дней) < NOW() → документ удаляется
        // 
        // Преимущества:
        // - Не нужен CRON
        // - Не блокирует таблицу
        // - Работает в фоне
        // ====================================================================
        
        var ttlIndex = Builders<MongoMessage>.IndexKeys
            .Ascending(m => m.SentAt);
        
        messagesCollection.Indexes.CreateOne(
            new CreateIndexModel<MongoMessage>(
                ttlIndex,
                new CreateIndexOptions 
                { 
                    Name = "IX_Messages_TTL",
                    ExpireAfter = TimeSpan.FromDays(30),  // Удалять через 30 дней
                    Background = true
                }
            )
        );
        
        // ====================================================================
        // INDEX 5: sender.userId (для поиска сообщений от пользователя)
        // ====================================================================
        // Вложенные поля индексируются через точку: "sender.userId"
        // 
        // Запрос:
        //   db.messages.find({ "sender.userId": 100 })
        // 
        // Используется для:
        // - Показать все сообщения пользователя
        // - Статистика активности
        // ====================================================================
        
        var senderIndex = Builders<MongoMessage>.IndexKeys
            .Ascending("sender.userId");  // Строка для вложенного поля!
        
        messagesCollection.Indexes.CreateOne(
            new CreateIndexModel<MongoMessage>(
                senderIndex,
                new CreateIndexOptions 
                { 
                    Name = "IX_Messages_SenderId",
                    Background = true
                }
            )
        );
    }
    
    // ========================================================================
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
    // ========================================================================
    
    /// <summary>
    /// Проверить подключение к MongoDB
    /// 
    /// Использование:
    ///   if (await context.IsConnectedAsync())
    ///       Console.WriteLine("MongoDB connected!");
    /// </summary>
    public async Task<bool> IsConnectedAsync()
    {
        try
        {
            // Пытаемся выполнить простую команду ping
            await _database.RunCommandAsync((Command<BsonDocument>)"{ping:1}");
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Получить список всех индексов для коллекции messages
    /// 
    /// Использование:
    ///   var indexes = await context.GetMessagesIndexesAsync();
    ///   foreach (var index in indexes)
    ///       Console.WriteLine(index);
    /// </summary>
    public async Task<List<string>> GetMessagesIndexesAsync()
    {
        var indexes = await Messages.Indexes.List().ToListAsync();
        return indexes
            .Select(idx => idx["name"].AsString)
            .ToList();
    }
    
    /// <summary>
    /// Удалить ВСЕ сообщения из коллекции (ОСТОРОЖНО!)
    /// 
    /// Использование:
    ///   await context.ClearMessagesAsync();  // Удалит все сообщения!
    /// 
    /// Используй ТОЛЬКО для тестирования!
    /// </summary>
    public async Task ClearMessagesAsync()
    {
        await Messages.DeleteManyAsync(m => true);  // true = все документы
    }
}

/*
 * ============================================================================
 * КАК ИСПОЛЬЗОВАТЬ MongoDbContext?
 * ============================================================================
 * 
 * 1. ИНИЦИАЛИЗАЦИЯ (в Program.cs):
 * 
 *    var mongoContext = new MongoDbContext(
 *        connectionString: "mongodb://localhost:27017",
 *        databaseName: "uchat"
 *    );
 *    
 *    // Проверка подключения
 *    if (await mongoContext.IsConnectedAsync())
 *        Console.WriteLine("MongoDB connected!");
 * 
 * 
 * 2. ВСТАВКА СООБЩЕНИЯ:
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
 *        Content = "Hello world!",
 *        Type = "text"
 *    };
 *    
 *    await mongoContext.Messages.InsertOneAsync(message);
 * 
 * 
 * 3. ПОЛУЧЕНИЕ ПОСЛЕДНИХ 50 СООБЩЕНИЙ:
 * 
 *    var messages = await mongoContext.Messages
 *        .Find(m => m.ChatId == 1)           // WHERE chatId = 1
 *        .SortByDescending(m => m.SentAt)    // ORDER BY sentAt DESC
 *        .Limit(50)                          // LIMIT 50
 *        .ToListAsync();
 * 
 * 
 * 4. ДОБАВЛЕНИЕ РЕАКЦИИ (АТОМАРНО!):
 * 
 *    var filter = Builders<MongoMessage>.Filter.Eq(m => m.Id, messageId);
 *    var update = Builders<MongoMessage>.Update
 *        .AddToSet("reactions.👍", userId);  // Добавить userId в массив
 *    
 *    await mongoContext.Messages.UpdateOneAsync(filter, update);
 * 
 * 
 * 5. ПОМЕТИТЬ КАК ПРОЧИТАННОЕ:
 * 
 *    var filter = Builders<MongoMessage>.Filter.Eq(m => m.Id, messageId);
 *    var update = Builders<MongoMessage>.Update
 *        .AddToSet(m => m.ReadBy, userId);
 *    
 *    await mongoContext.Messages.UpdateOneAsync(filter, update);
 * 
 * 
 * 6. УДАЛЕНИЕ СООБЩЕНИЯ (SOFT DELETE):
 * 
 *    var filter = Builders<MongoMessage>.Filter.Eq(m => m.Id, messageId);
 *    var update = Builders<MongoMessage>.Update
 *        .Set(m => m.IsDeleted, true);
 *    
 *    await mongoContext.Messages.UpdateOneAsync(filter, update);
 * 
 * 
 * 7. ПОЛУЧЕНИЕ СТАТИСТИКИ:
 * 
 *    // Сколько всего сообщений в чате?
 *    var count = await mongoContext.Messages
 *        .CountDocumentsAsync(m => m.ChatId == 1);
 *    
 *    // Сколько непрочитанных?
 *    var unreadCount = await mongoContext.Messages
 *        .CountDocumentsAsync(m => 
 *            m.ChatId == 1 && 
 *            !m.ReadBy.Contains(userId)
 *        );
 * 
 * ============================================================================
 * DEPENDENCY INJECTION (для реальных приложений)
 * ============================================================================
 * 
 * В Program.cs:
 * 
 *   builder.Services.AddSingleton<MongoDbContext>(sp =>
 *   {
 *       var connectionString = builder.Configuration.GetConnectionString("MongoDB");
 *       return new MongoDbContext(connectionString, "uchat");
 *   });
 * 
 * 
 * В сервисах:
 * 
 *   public class MessageService
 *   {
 *       private readonly MongoDbContext _mongoContext;
 *       
 *       public MessageService(MongoDbContext mongoContext)
 *       {
 *           _mongoContext = mongoContext;
 *       }
 *       
 *       public async Task<List<MongoMessage>> GetChatMessagesAsync(int chatId)
 *       {
 *           return await _mongoContext.Messages
 *               .Find(m => m.ChatId == chatId)
 *               .SortByDescending(m => m.SentAt)
 *               .Limit(50)
 *               .ToListAsync();
 *       }
 *   }
 * 
 * ============================================================================
 */
