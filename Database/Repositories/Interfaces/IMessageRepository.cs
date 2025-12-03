/*
 * ============================================================================
 * REPOSITORY INTERFACE: Message Repository
 * ============================================================================
 * 
 * ПАТТЕРН REPOSITORY
 * 
 * Зачем нужен репозиторий?
 * 1. Абстракция доступа к данным
 * 2. Легко тестировать (можно подменить на mock)
 * 3. Бизнес-логика не зависит от MongoDB напрямую
 * 4. Легко сменить БД (с MongoDB на другую)
 * 
 * Пример использования:
 *   IMessageRepository repo = new MessageRepository(context);
 *   var messages = await repo.GetChatMessagesAsync(chatId, limit: 50);
 * 
 * ============================================================================
 */

using Uchat.Database.MongoDB;

namespace Uchat.Database.Repositories.Interfaces;

/// <summary>
/// Интерфейс репозитория для работы с сообщениями в MongoDB
/// 
/// Предоставляет методы:
/// - Получение истории чата (READ)
/// - Обновление (редактирование, удаление) - требует проверки прав в вызывающем коде
/// - Реакции - доступны всем участникам чата
/// - Статус прочтения - доступен всем участникам
/// 
/// ⚠️ ВАЖНО: Создание новых сообщений ТОЛЬКО через MessageService!
/// MessageService обеспечивает валидацию, проверку прав и координацию SQLite + MongoDB.
/// </summary>
public interface IMessageRepository
{
    // ========================================================================
    // ПОЛУЧЕНИЕ СООБЩЕНИЙ (READ OPERATIONS)
    // ========================================================================
    
    // ========================================================================
    // ПОЛУЧЕНИЕ СООБЩЕНИЙ
    // ========================================================================
    
    /// <summary>
    /// Получить последние N сообщений из чата (CURSOR-BASED PAGINATION)
    /// 
    /// Параметры:
    /// - chatId: ID чата (из SQLite ChatRooms.Id)
    /// - limit: сколько сообщений вернуть (по умолчанию 50)
    /// - lastTimestamp: время последнего сообщения (для пагинации)
    ///   
    ///   null = первая загрузка (последние 50 сообщений)
    ///   DateTime = загрузить старые сообщения до этого времени
    /// 
    /// Возвращает: список сообщений, отсортированных по времени (новые первыми)
    /// 
    /// SQL аналог:
    ///   -- Первая загрузка:
    ///   SELECT * FROM messages 
    ///   WHERE chatId = @chatId AND isDeleted = false
    ///   ORDER BY sentAt DESC 
    ///   LIMIT @limit
    ///   
    ///   -- Загрузить еще:
    ///   SELECT * FROM messages 
    ///   WHERE chatId = @chatId AND isDeleted = false AND sentAt < @lastTimestamp
    ///   ORDER BY sentAt DESC 
    ///   LIMIT @limit
    /// 
    /// Использует составной индекс (ChatId, SentAt DESC) для мгновенной загрузки
    /// 
    /// Пример:
    ///   // Первая загрузка (последние 50 сообщений)
    ///   var messages = await repo.GetChatMessagesAsync(chatId: 1, limit: 50);
    ///   var lastTimestamp = messages.Last().SentAt;
    ///   
    ///   // Загрузить еще 50 (старые сообщения)
    ///   var olderMessages = await repo.GetChatMessagesAsync(chatId: 1, limit: 50, lastTimestamp: lastTimestamp);
    /// 
    /// Преимущества CURSOR-BASED над OFFSET-BASED:
    /// ✅ Мгновенный поиск по индексу (O(log n))
    /// ✅ Стабильные результаты (новые сообщения не влияют)
    /// ✅ Поддержка бесконечной прокрутки
    /// 
    /// ❌ OFFSET-BASED (старый способ):
    ///   - Медленно на больших offset (сканирует все пропущенные строки)
    ///   - Пропускает новые сообщения (непредсказуемо)
    /// </summary>
    Task<List<MongoMessage>> GetChatMessagesAsync(int chatId, int limit = 50, DateTime? lastTimestamp = null);
    
    /// <summary>
    /// Получить сообщение по ID
    /// 
    /// Параметры:
    /// - messageId: ObjectId сообщения
    /// 
    /// Возвращает: сообщение или null если не найдено
    /// 
    /// Пример:
    ///   var message = await repo.GetMessageByIdAsync("507f1f77bcf86cd799439011");
    /// </summary>
    Task<MongoMessage?> GetMessageByIdAsync(string messageId);
    
    /// <summary>
    /// Получить непрочитанные сообщения для пользователя
    /// 
    /// Параметры:
    /// - chatId: ID чата
    /// - userId: ID пользователя (из SQLite Users.Id)
    /// 
    /// Возвращает: список сообщений, которые пользователь еще не прочитал
    /// 
    /// MongoDB запрос:
    ///   db.messages.find({ 
    ///     chatId: @chatId, 
    ///     readBy: { $ne: @userId }  // userId НЕ в массиве readBy
    ///   })
    /// 
    /// Пример:
    ///   var unread = await repo.GetUnreadMessagesAsync(chatId: 1, userId: 100);
    /// </summary>
    Task<List<MongoMessage>> GetUnreadMessagesAsync(int chatId, int userId);
    
    /// <summary>
    /// Получить количество непрочитанных сообщений
    /// 
    /// Параметры:
    /// - chatId: ID чата
    /// - userId: ID пользователя
    /// 
    /// Возвращает: число непрочитанных сообщений
    /// 
    /// Пример:
    ///   var count = await repo.GetUnreadCountAsync(chatId: 1, userId: 100);
    ///   // count = 5 (5 непрочитанных сообщений)
    /// </summary>
    Task<long> GetUnreadCountAsync(int chatId, int userId);
    
    // ========================================================================
    // РЕДАКТИРОВАНИЕ И УДАЛЕНИЕ
    // ========================================================================
    
    /// <summary>
    /// Отредактировать текст сообщения
    /// 
    /// ⚠️ ПРОВЕРКА ПРАВ: Вызывающий код ДОЛЖЕН проверить права перед вызовом!
    /// - Только автор сообщения может редактировать
    /// - Или админ чата с правом CanDeleteMessages()
    /// 
    /// Параметры:
    /// - messageId: ID сообщения
    /// - newContent: новый текст
    /// 
    /// Возвращает: true если успешно, false если сообщение не найдено
    /// 
    /// Пример использования в API:
    ///   [HttpPatch("api/messages/{id}")]
    ///   public async Task<IActionResult> EditMessage(string id, EditDto dto)
    ///   {
    ///       var message = await _messageRepo.GetMessageByIdAsync(id);
    ///       if (message == null) return NotFound();
    ///       
    ///       var userId = GetCurrentUserId();
    ///       if (message.Sender.UserId != userId)
    ///       {
    ///           var member = await _chatRepo.GetMemberAsync(message.ChatId, userId);
    ///           if (member == null || !member.CanDeleteMessages()) return Forbid();
    ///       }
    ///       
    ///       await _messageRepo.EditMessageAsync(id, dto.Content);
    ///       return NoContent();
    ///   }
    /// </summary>
    Task<bool> EditMessageAsync(string messageId, string newContent);
    
    /// <summary>
    /// Удалить сообщение (soft delete)
    /// 
    /// ⚠️ ПРОВЕРКА ПРАВ: Вызывающий код ДОЛЖЕН проверить права перед вызовом!
    /// - Только автор сообщения может удалить
    /// - Или админ/модератор чата с правом CanDeleteMessages()
    /// 
    /// Параметры:
    /// - messageId: ID сообщения
    /// 
    /// Возвращает: true если успешно
    /// 
    /// НЕ УДАЛЯЕТ физически! Устанавливает isDeleted = true
    /// 
    /// Пример использования в API:
    ///   [HttpDelete("api/messages/{id}")]
    ///   public async Task<IActionResult> DeleteMessage(string id)
    ///   {
    ///       var message = await _messageRepo.GetMessageByIdAsync(id);
    ///       if (message == null) return NotFound();
    ///       
    ///       var userId = GetCurrentUserId();
    ///       var isAuthor = message.Sender.UserId == userId;
    ///       
    ///       if (!isAuthor)
    ///       {
    ///           var member = await _chatRepo.GetMemberAsync(message.ChatId, userId);
    ///           if (member == null || !member.CanDeleteMessages()) return Forbid();
    ///       }
    ///       
    ///       await _messageRepo.DeleteMessageAsync(id);
    ///       return NoContent();
    ///   }
    /// </summary>
    Task<bool> DeleteMessageAsync(string messageId);
    
    // ========================================================================
    // РЕАКЦИИ
    // ========================================================================
    
    /// <summary>
    /// Добавить реакцию к сообщению
    /// 
    /// Параметры:
    /// - messageId: ID сообщения
    /// - emoji: эмодзи реакции (👍, ❤️, 😂, etc.)
    /// - userId: ID пользователя
    /// 
    /// Возвращает: true если успешно
    /// 
    /// АТОМАРНАЯ операция:
    ///   db.messages.updateOne(
    ///     { _id: messageId },
    ///     { $addToSet: { "reactions.👍": userId } }
    ///   )
    /// 
    /// $addToSet - добавляет элемент только если его еще нет
    /// (предотвращает дубликаты)
    /// 
    /// Пример:
    ///   await repo.AddReactionAsync(messageId, "👍", userId: 100);
    /// </summary>
    Task<bool> AddReactionAsync(string messageId, string emoji, int userId);
    
    /// <summary>
    /// Удалить реакцию
    /// 
    /// Параметры:
    /// - messageId: ID сообщения
    /// - emoji: эмодзи реакции
    /// - userId: ID пользователя
    /// 
    /// Возвращает: true если успешно
    /// 
    /// АТОМАРНАЯ операция:
    ///   db.messages.updateOne(
    ///     { _id: messageId },
    ///     { $pull: { "reactions.👍": userId } }
    ///   )
    /// 
    /// $pull - удаляет элемент из массива
    /// 
    /// Пример:
    ///   await repo.RemoveReactionAsync(messageId, "👍", userId: 100);
    /// </summary>
    Task<bool> RemoveReactionAsync(string messageId, string emoji, int userId);
    
    // ========================================================================
    // СТАТУС ПРОЧТЕНИЯ
    // ========================================================================
    
    /// <summary>
    /// Пометить сообщение как прочитанное
    /// 
    /// Параметры:
    /// - messageId: ID сообщения
    /// - userId: ID пользователя
    /// 
    /// Возвращает: true если успешно
    /// 
    /// АТОМАРНАЯ операция:
    ///   db.messages.updateOne(
    ///     { _id: messageId },
    ///     { $addToSet: { readBy: userId } }
    ///   )
    /// 
    /// Пример:
    ///   await repo.MarkAsReadAsync(messageId, userId: 100);
    /// </summary>
    Task<bool> MarkAsReadAsync(string messageId, int userId);
    
    /// <summary>
    /// Пометить все сообщения в чате как прочитанные
    /// 
    /// Параметры:
    /// - chatId: ID чата
    /// - userId: ID пользователя
    /// 
    /// Возвращает: количество обновленных сообщений
    /// 
    /// MongoDB операция:
    ///   db.messages.updateMany(
    ///     { 
    ///       chatId: @chatId, 
    ///       readBy: { $ne: @userId }  // userId НЕ в readBy
    ///     },
    ///     { $addToSet: { readBy: @userId } }
    ///   )
    /// 
    /// Пример:
    ///   var count = await repo.MarkAllAsReadAsync(chatId: 1, userId: 100);
    ///   // count = 15 (помечено 15 сообщений)
    /// </summary>
    Task<long> MarkAllAsReadAsync(int chatId, int userId);
    
    /// <summary>
    /// Пометить все сообщения до указанного времени как прочитанные
    /// 
    /// ⚠️ ОПТИМАЛЬНЫЙ ПОДХОД для массового прочтения!
    /// Вместо отправки массива ID, клиент отправляет timestamp последнего видимого сообщения.
    /// 
    /// Параметры:
    /// - chatId: ID чата
    /// - userId: ID пользователя
    /// - untilTimestamp: помечает все сообщения ДО этого времени (включительно)
    /// 
    /// Возвращает: количество обновленных сообщений
    /// 
    /// Пример использования:
    ///   // Клиент прокрутил чат и видит сообщения до 14:30
    ///   var lastVisible = DateTime.Parse("2024-01-15T14:30:00Z");
    ///   var count = await repo.MarkAsReadUntilAsync(chatId: 1, userId: 100, untilTimestamp: lastVisible);
    ///   // count = 25 (помечено 25 сообщений одним запросом)
    /// 
    /// Преимущества:
    /// ✅ Один запрос вместо N запросов
    /// ✅ Использует индекс (chatId, sentAt)
    /// ✅ Не нужно передавать массив ID (экономит трафик)
    /// ✅ Автоматически учитывает новые сообщения
    /// </summary>
    Task<long> MarkAsReadUntilAsync(int chatId, int userId, DateTime untilTimestamp);
    
    // ========================================================================
    // ПОИСК
    // ========================================================================
    
    /// <summary>
    /// Поиск сообщений по тексту
    /// 
    /// Параметры:
    /// - chatId: ID чата
    /// - searchQuery: поисковый запрос
    /// - limit: максимум результатов
    /// 
    /// Возвращает: список найденных сообщений
    /// 
    /// MongoDB запрос:
    ///   db.messages.find({ 
    ///     chatId: @chatId,
    ///     content: { $regex: @searchQuery, $options: 'i' }  // case-insensitive
    ///   })
    /// 
    /// Пример:
    ///   var results = await repo.SearchMessagesAsync(chatId: 1, "hello");
    /// </summary>
    Task<List<MongoMessage>> SearchMessagesAsync(int chatId, string searchQuery, int limit = 20);
}

/*
 * ============================================================================
 * ПОЧЕМУ ИНТЕРФЕЙС?
 * ============================================================================
 * 
 * 1. ТЕСТИРОВАНИЕ
 *    
 *    public class MessageServiceTests
 *    {
 *        [Test]
 *        public async Task SendMessage_ShouldReturnMessageId()
 *        {
 *            // Создаем MOCK репозиторий
 *            var mockRepo = new Mock<IMessageRepository>();
 *            mockRepo.Setup(r => r.SendMessageAsync(It.IsAny<LiteDbMessage>()))
 *                    .ReturnsAsync("507f1f77bcf86cd799439011");
 *            
 *            // Тестируем сервис БЕЗ реальной БД!
 *            var service = new MessageService(mockRepo.Object);
 *            var result = await service.SendMessageAsync(...);
 *            
 *            Assert.IsNotNull(result);
 *        }
 *    }
 * 
 * 
 * 2. DEPENDENCY INJECTION
 *    
 *    // Program.cs
 *    builder.Services.AddScoped<IMessageRepository, MessageRepository>();
 *    
 *    // В контроллере
 *    public class MessagesController : ControllerBase
 *    {
 *        private readonly IMessageRepository _repo;
 *        
 *        public MessagesController(IMessageRepository repo)
 *        {
 *            _repo = repo;  // Автоматически инжектится!
 *        }
 *    }
 * 
 * 
 * 3. ЛЕГКО СМЕНИТЬ РЕАЛИЗАЦИЮ
 *    
 *    // Было: LiteDB
 *    builder.Services.AddScoped<IMessageRepository, MessageRepository>();
 *    
 *    // Стало: другая БД
 *    builder.Services.AddScoped<IMessageRepository, OtherDbMessageRepository>();
 *    
 *    // Код контроллеров НЕ МЕНЯЕТСЯ!
 * 
 * ============================================================================
 */
