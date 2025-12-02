using LiteDB;
using Uchat.Database.LiteDB;

namespace Uchat.Database.LiteDB;

/// <summary>
/// LiteDB context for message storage
/// Provides access to collections and manages indexes
/// </summary>
public class LiteDbContext : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly string _databasePath;
    private static readonly object _indexLock = new object();
    private static bool _indexesInitialized = false;
    
    public LiteDbContext(LiteDbSettings settings)
    {
        _databasePath = settings.DatabasePath;
        var connectionString = $"Filename={_databasePath};Connection=shared";
        _database = new LiteDatabase(connectionString);
        InitializeIndexes();
    }
    
    public LiteDbContext(string databasePath)
    {
        _databasePath = databasePath;
        _database = new LiteDatabase(_databasePath);
        InitializeIndexes();
    }
    
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
    /// Thread-safe благодаря lock и статическому флагу
    /// </summary>
    private void InitializeIndexes()
    {
        // Проверяем флаг БЕЗ lock (быстрая проверка)
        if (_indexesInitialized)
            return;
        
        // Блокируем для thread-safe инициализации
        lock (_indexLock)
        {
            // Повторная проверка ВНУТРИ lock (double-check locking)
            if (_indexesInitialized)
                return;
            
            CreateMessagesIndexes();
            _indexesInitialized = true;
        }
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
        
        // LiteDB не поддерживает составные индексы с DESC в одном выражении
        // Создаём два отдельных индекса
        messagesCollection.EnsureIndex(m => m.ChatId);
        messagesCollection.EnsureIndex(m => m.SentAt);
        
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
    
    public bool DatabaseExists() => File.Exists(_databasePath);
    
    public IEnumerable<string> GetCollectionNames() => _database.GetCollectionNames();
    
    public int ClearMessages() => Messages.DeleteAll();
    
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
    
    public void Dispose()
    {
        _database?.Dispose();
        GC.SuppressFinalize(this);
    }
}
