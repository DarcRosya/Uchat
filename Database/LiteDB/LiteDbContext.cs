/*
 * ============================================================================
 * LITEDB CONTEXT (Контекст базы данных LiteDB)
 * ============================================================================
 * 
 * ЧТО ТАКОЕ LiteDbContext?
 * 
 * Это аналог MongoDbContext, но для LiteDB:
 * 1. Подключение к локальному файлу LiteDB
 * 2. Доступ к коллекциям (аналог DbSet<T>)
 * 3. Настройка индексов
 * 4. Легковесная NoSQL база данных для .NET
 * 
 * ============================================================================
 * ОТЛИЧИЕ ОТ MONGODB
 * ============================================================================
 * 
 * MongoDB:
 *   - Серверная база данных (требует MongoDB Server)
 *   - Облачное решение (MongoDB Atlas)
 *   - Подключение через connection string
 *   - TTL индексы для автоудаления
 * 
 * LiteDB:
 *   - Встраиваемая база данных (один файл .db)
 *   - Локальное решение (как SQLite)
 *   - Прямой доступ к файлу
 *   - Ручное управление удалением старых данных
 * 
 * ============================================================================
 * ПРЕИМУЩЕСТВА LITEDB
 * ============================================================================
 * 
 * 1. Не требует установки сервера (один файл)
 * 2. 100% управляемый C# код (без внешних зависимостей)
 * 3. ACID транзакции
 * 4. Индексы для быстрого поиска
 * 5. Поддержка LINQ запросов
 * 6. Размер БД до 2 ТБ
 * 
 * ============================================================================
 */

using LiteDB;
using Database.LiteDB;

namespace Database.LiteDB;

/// <summary>
/// Контекст для работы с LiteDB
/// 
/// Предоставляет доступ к коллекциям:
/// - Messages (сообщения в чатах)
/// 
/// Автоматически создает индексы при инициализации
/// </summary>
public class LiteDbContext : IDisposable
{
    // ========================================================================
    // ПОЛЯ
    // ========================================================================
    
    /// <summary>
    /// База данных LiteDB
    /// Singleton - создается один раз и переиспользуется
    /// </summary>
    private readonly LiteDatabase _database;
    
    /// <summary>
    /// Путь к файлу базы данных
    /// </summary>
    private readonly string _databasePath;
    
    // ========================================================================
    // КОНСТРУКТОР
    // ========================================================================
    
    /// <summary>
    /// Инициализация LiteDB контекста через настройки
    /// 
    /// Использование (с Dependency Injection):
    ///   services.Configure<LiteDbSettings>(configuration.GetSection("LiteDb"));
    ///   services.AddSingleton<LiteDbContext>();
    /// </summary>
    public LiteDbContext(LiteDbSettings settings)
    {
        // 1. Сохраняем путь к БД
        _databasePath = settings.DatabasePath;
        
        // 2. Создаем подключение к LiteDB
        //    Если файл не существует - создастся автоматически
        _database = new LiteDatabase(_databasePath);
        
        // 3. Создаем индексы для коллекций
        //    Это нужно сделать ОДИН РАЗ при первом запуске
        //    Повторный вызов безопасен (индексы не дублируются)
        InitializeIndexes();
    }
    
    /// <summary>
    /// Инициализация LiteDB контекста (legacy)
    /// 
    /// Параметры:
    /// - databasePath: путь к файлу базы данных
    ///   Примеры:
    ///     "messages.db"
    ///     "Data/messages.db"
    ///     "C:/Databases/messages.db"
    /// </summary>
    public LiteDbContext(string databasePath)
    {
        // 1. Сохраняем путь к БД
        _databasePath = databasePath;
        
        // 2. Создаем подключение к LiteDB
        //    Если файл не существует - создастся автоматически
        _database = new LiteDatabase(_databasePath);
        
        // 3. Создаем индексы для коллекций
        //    Это нужно сделать ОДИН РАЗ при первом запуске
        //    Повторный вызов безопасен (индексы не дублируются)
        InitializeIndexes();
    }
    
    // ========================================================================
    // КОЛЛЕКЦИИ (аналог DbSet<T>)
    // ========================================================================
    // В LiteDB данные хранятся в КОЛЛЕКЦИЯХ
    // Коллекция = аналог таблицы в SQL
    // 
    // LiteDB автоматически:
    // - Создает коллекцию при первой вставке
    // - Поддерживает BSON формат (как MongoDB)
    // - Использует ObjectId для _id
    // ========================================================================
    
    /// <summary>
    /// Коллекция сообщений
    /// В LiteDB: "messages"
    /// 
    /// Использование:
    ///   context.Messages.Insert(message);
    ///   var messages = context.Messages.Find(m => m.ChatId == 1).ToList();
    /// 
    /// Аналог SQL:
    ///   INSERT INTO messages ...
    ///   SELECT * FROM messages WHERE chatId = 1
    /// </summary>
    public ILiteCollection<LiteDbMessage> Messages => 
        _database.GetCollection<LiteDbMessage>("messages");
    
    // ========================================================================
    // ИНИЦИАЛИЗАЦИЯ ИНДЕКСОВ
    // ========================================================================
    // LiteDB РЕКОМЕНДУЕТ создавать индексы вручную
    // 
    // Зачем нужны индексы?
    // - Ускоряют поиск (WHERE chatId = 1)
    // - Ускоряют сортировку (ORDER BY sentAt DESC)
    // - Гарантируют уникальность (UNIQUE INDEX)
    // 
    // БЕЗ индексов LiteDB сканирует ВСЮ коллекцию (медленно!)
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
    /// 1. Composite (ChatId, SentAt DESC) - для cursor-based pagination
    /// 2. ChatId (для поиска сообщений в чате)
    /// 3. SentAt (для сортировки по времени)
    /// 4. Sender.UserId (для поиска сообщений от пользователя)
    /// </summary>
    private void CreateMessagesIndexes()
    {
        var messagesCollection = Messages;
        
        // ====================================================================
        // INDEX 1: COMPOSITE (ChatId, SentAt DESC) - ДЛЯ ПАГИНАЦИИ
        // ====================================================================
        // SQL аналог: CREATE INDEX IX_Messages_ChatId_SentAt 
        //             ON messages(chatId ASC, sentAt DESC)
        // 
        // CURSOR-BASED PAGINATION (пагинация по времени):
        // 
        //   1. Первая загрузка (последние 50 сообщений):
        //      var messages = collection
        //          .Find(m => m.ChatId == chatId)
        //          .OrderByDescending(m => m.SentAt)
        //          .Limit(50)
        //          .ToList();
        //      
        //      var lastTimestamp = messages.Last().SentAt;
        //   
        //   2. Загрузить еще 50 (старые сообщения):
        //      var olderMessages = collection
        //          .Find(m => m.ChatId == chatId && m.SentAt < lastTimestamp)
        //          .OrderByDescending(m => m.SentAt)
        //          .Limit(50)
        //          .ToList();
        // 
        // ЧЕМ ЛУЧШЕ OFFSET-BASED?
        // ❌ OFFSET: SELECT * FROM messages WHERE chatId = 1 
        //            ORDER BY sentAt DESC LIMIT 50 OFFSET 100
        //    - Медленно на больших offset (сканирует ВСЕ пропущенные строки)
        //    - Пропускает новые сообщения (непредсказуемо)
        // 
        // ✅ CURSOR: SELECT * FROM messages WHERE chatId = 1 
        //            AND sentAt < lastTimestamp 
        //            ORDER BY sentAt DESC LIMIT 50
        //    - Мгновенный поиск по индексу (O(log n))
        //    - Стабильные результаты (новые сообщения не влияют)
        // 
        // СОСТАВНОЙ ИНДЕКС позволяет LiteDB:
        // - Сразу найти диапазон chatId (B-tree lookup)
        // - Внутри диапазона уже отсортировано по sentAt DESC
        // - Взять первые 50 без дополнительной сортировки
        // ====================================================================
        
        messagesCollection.EnsureIndex("ChatId_SentAt_Idx", "$.ChatId, $.SentAt DESC");
        
        // ====================================================================
        // INDEX 2: ChatId (для быстрого поиска сообщений в конкретном чате)
        // ====================================================================
        // SQL аналог: CREATE INDEX IX_Messages_ChatId ON messages(chatId)
        // 
        // Запрос:
        //   messages.Find(m => m.ChatId == 1)
        // 
        // Без индекса: сканирует ВСЮ коллекцию (медленно!)
        // С индексом: мгновенный поиск через B-tree
        // ====================================================================
        
        messagesCollection.EnsureIndex(m => m.ChatId);
        
        // ====================================================================
        // INDEX 3: SentAt (для сортировки по времени)
        // ====================================================================
        // SQL аналог: CREATE INDEX IX_Messages_SentAt ON messages(sentAt)
        // 
        // Запрос:
        //   messages.Find(Query.All("SentAt", Query.Descending))
        // 
        // Используется для:
        // - Сортировка "сначала новые" (ORDER BY sentAt DESC)
        // - Поиск сообщений за период (WHERE sentAt > date)
        // ====================================================================
        
        messagesCollection.EnsureIndex(m => m.SentAt);
        
        // ====================================================================
        // INDEX 4: Sender.UserId (для поиска сообщений от пользователя)
        // ====================================================================
        // Вложенные поля индексируются через лямбду
        // 
        // Запрос:
        //   messages.Find(m => m.Sender.UserId == 100)
        // 
        // Используется для:
        // - Показать все сообщения пользователя
        // - Статистика активности
        // ====================================================================
        
        messagesCollection.EnsureIndex(m => m.Sender.UserId);
    }
    
    // ========================================================================
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
    // ========================================================================
    
    public bool DatabaseExists()
    {
        return File.Exists(_databasePath);
    }
    public IEnumerable<string> GetCollectionNames()
    {
        return _database.GetCollectionNames();
    }
    
    /// <summary>
    /// Удалить ВСЕ сообщения из коллекции (ОСТОРОЖНО!)
    /// 
    /// Использование:
    ///   context.ClearMessages();  // Удалит все сообщения!
    /// 
    /// Используй ТОЛЬКО для тестирования!
    /// </summary>
    public int ClearMessages()
    {
        return Messages.DeleteAll();
    }
    
    /// Удалить старые сообщения (альтернатива TTL индексу MongoDB)
    /// 
    /// Использование:
    ///   context.DeleteOldMessages(30);  // Удалить сообщения старше 30 дней
    public int DeleteOldMessages(int daysOld)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
        return Messages.DeleteMany(m => m.SentAt < cutoffDate);
    }
    public long GetDatabaseSize()
    {
        var fileInfo = new FileInfo(_databasePath);
        return fileInfo.Exists ? fileInfo.Length : 0;
    }
    
    // ========================================================================
    // DISPOSE PATTERN
    // ========================================================================
    
    /// <summary>
    /// Освобождение ресурсов
    /// Закрывает соединение с базой данных
    /// </summary>
    public void Dispose()
    {
        _database?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/*
 * ============================================================================
 * КАК ИСПОЛЬЗОВАТЬ LiteDbContext?
 * ============================================================================
 * 
 * 1. ИНИЦИАЛИЗАЦИЯ (в Program.cs):
 * 
 *    var liteDbContext = new LiteDbContext("Data/messages.db");
 *    
 *    // Проверка существования БД
 *    if (liteDbContext.DatabaseExists())
 *        Console.WriteLine("LiteDB database exists!");
 * 
 * 
 * 2. ВСТАВКА СООБЩЕНИЯ:
 * 
 *    var message = new LiteDbMessage
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
 *    liteDbContext.Messages.Insert(message);
 * 
 * 
 * 3. ПОЛУЧЕНИЕ ПОСЛЕДНИХ 50 СООБЩЕНИЙ:
 * 
 *    var messages = liteDbContext.Messages
 *        .Find(m => m.ChatId == 1)           // WHERE chatId = 1
 *        .OrderByDescending(m => m.SentAt)   // ORDER BY sentAt DESC
 *        .Limit(50)                          // LIMIT 50
 *        .ToList();
 * 
 * 
 * 4. ОБНОВЛЕНИЕ СООБЩЕНИЯ:
 * 
 *    var message = liteDbContext.Messages
 *        .FindById(messageId);
 *    
 *    message.Content = "Updated content";
 *    message.EditedAt = DateTime.UtcNow;
 *    
 *    liteDbContext.Messages.Update(message);
 * 
 * 
 * 5. ДОБАВЛЕНИЕ РЕАКЦИИ:
 * 
 *    var message = liteDbContext.Messages.FindById(messageId);
 *    
 *    if (!message.Reactions.ContainsKey("👍"))
 *        message.Reactions["👍"] = new List<int>();
 *    
 *    if (!message.Reactions["👍"].Contains(userId))
 *        message.Reactions["👍"].Add(userId);
 *    
 *    liteDbContext.Messages.Update(message);
 * 
 * 
 * 6. ПОМЕТИТЬ КАК ПРОЧИТАННОЕ:
 * 
 *    var message = liteDbContext.Messages.FindById(messageId);
 *    
 *    if (!message.ReadBy.Contains(userId))
 *    {
 *        message.ReadBy.Add(userId);
 *        liteDbContext.Messages.Update(message);
 *    }
 * 
 * 
 * 7. УДАЛЕНИЕ СООБЩЕНИЯ (SOFT DELETE):
 * 
 *    var message = liteDbContext.Messages.FindById(messageId);
 *    message.IsDeleted = true;
 *    liteDbContext.Messages.Update(message);
 * 
 * 
 * 8. ПОЛУЧЕНИЕ СТАТИСТИКИ:
 * 
 *    // Сколько всего сообщений в чате?
 *    var count = liteDbContext.Messages
 *        .Count(m => m.ChatId == 1);
 *    
 *    // Сколько непрочитанных?
 *    var unreadCount = liteDbContext.Messages
 *        .Count(m => m.ChatId == 1 && !m.ReadBy.Contains(userId));
 * 
 * ============================================================================
 * DEPENDENCY INJECTION (для реальных приложений)
 * ============================================================================
 * 
 * В Program.cs:
 * 
 *   builder.Services.Configure<LiteDbSettings>(
 *       builder.Configuration.GetSection("LiteDb"));
 *       
 *   builder.Services.AddSingleton<LiteDbContext>(sp =>
 *   {
 *       var settings = sp.GetRequiredService<IOptions<LiteDbSettings>>().Value;
 *       return new LiteDbContext(settings);
 *   });
 * 
 * 
 * В сервисах:
 * 
 *   public class MessageService
 *   {
 *       private readonly LiteDbContext _liteDbContext;
 *       
 *       public MessageService(LiteDbContext liteDbContext)
 *       {
 *           _liteDbContext = liteDbContext;
 *       }
 *       
 *       public List<LiteDbMessage> GetChatMessages(int chatId)
 *       {
 *           return _liteDbContext.Messages
 *               .Find(m => m.ChatId == chatId)
 *               .OrderByDescending(m => m.SentAt)
 *               .Limit(50)
 *               .ToList();
 *       }
 *   }
 * 
 * ============================================================================
 * АВТОУДАЛЕНИЕ СТАРЫХ СООБЩЕНИЙ
 * ============================================================================
 * 
 * LiteDB не поддерживает TTL индексы (как MongoDB)
 * Используй BackgroundService для очистки:
 * 
 *   public class MessageCleanupService : BackgroundService
 *   {
 *       private readonly LiteDbContext _context;
 *       
 *       public MessageCleanupService(LiteDbContext context)
 *       {
 *           _context = context;
 *       }
 *       
 *       protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 *       {
 *           while (!stoppingToken.IsCancellationRequested)
 *           {
 *               // Удалить сообщения старше 30 дней
 *               var deleted = _context.DeleteOldMessages(30);
 *               Console.WriteLine($"Deleted {deleted} old messages");
 *               
 *               // Запускать раз в день
 *               await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
 *           }
 *       }
 *   }
 *   
 *   // В Program.cs:
 *   builder.Services.AddHostedService<MessageCleanupService>();
 * 
 * ============================================================================
 */
