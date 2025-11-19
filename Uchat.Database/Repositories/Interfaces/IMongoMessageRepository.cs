/*
 * ============================================================================
 * REPOSITORY INTERFACE: MongoDB Message Repository
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
 *   IMongoMessageRepository repo = new MongoMessageRepository(context);
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
/// - Создание сообщений
/// - Получение истории чата
/// - Обновление (редактирование, удаление)
/// - Реакции
/// - Статус прочтения
/// </summary>
public interface IMongoMessageRepository
{
    // ========================================================================
    // СОЗДАНИЕ СООБЩЕНИЙ
    // ========================================================================
    
    /// <summary>
    /// Отправить новое сообщение
    /// 
    /// Параметры:
    /// - message: готовый объект MongoMessage
    /// 
    /// Возвращает: ID созданного сообщения
    /// 
    /// Пример:
    ///   var message = new MongoMessage { ChatId = 1, Content = "Hello!" };
    ///   var messageId = await repo.SendMessageAsync(message);
    /// </summary>
    Task<string> SendMessageAsync(MongoMessage message);
    
    // ========================================================================
    // ПОЛУЧЕНИЕ СООБЩЕНИЙ
    // ========================================================================
    
    /// <summary>
    /// Получить последние N сообщений из чата
    /// 
    /// Параметры:
    /// - chatId: ID чата (из SQLite ChatRooms.Id)
    /// - limit: сколько сообщений вернуть (по умолчанию 50)
    /// - skip: сколько пропустить (для пагинации, по умолчанию 0)
    /// 
    /// Возвращает: список сообщений, отсортированных по времени (новые первыми)
    /// 
    /// SQL аналог:
    ///   SELECT * FROM messages 
    ///   WHERE chatId = @chatId 
    ///   ORDER BY sentAt DESC 
    ///   LIMIT @limit OFFSET @skip
    /// 
    /// Пример:
    ///   // Первые 50 сообщений
    ///   var messages = await repo.GetChatMessagesAsync(chatId: 1, limit: 50);
    ///   
    ///   // Следующие 50 (для "загрузить еще")
    ///   var moreMessages = await repo.GetChatMessagesAsync(chatId: 1, limit: 50, skip: 50);
    /// </summary>
    Task<List<MongoMessage>> GetChatMessagesAsync(int chatId, int limit = 50, int skip = 0);
    
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
    /// Параметры:
    /// - messageId: ID сообщения
    /// - newContent: новый текст
    /// 
    /// Возвращает: true если успешно, false если сообщение не найдено
    /// 
    /// MongoDB операция:
    ///   db.messages.updateOne(
    ///     { _id: messageId },
    ///     { 
    ///       $set: { 
    ///         content: newContent, 
    ///         editedAt: new Date() 
    ///       } 
    ///     }
    ///   )
    /// 
    /// Пример:
    ///   await repo.EditMessageAsync(messageId, "Updated text!");
    /// </summary>
    Task<bool> EditMessageAsync(string messageId, string newContent);
    
    /// <summary>
    /// Удалить сообщение (soft delete)
    /// 
    /// Параметры:
    /// - messageId: ID сообщения
    /// 
    /// Возвращает: true если успешно
    /// 
    /// НЕ УДАЛЯЕТ физически! Устанавливает isDeleted = true
    /// Сообщение будет автоматически удалено через 30 дней (TTL Index)
    /// 
    /// MongoDB операция:
    ///   db.messages.updateOne(
    ///     { _id: messageId },
    ///     { $set: { isDeleted: true } }
    ///   )
    /// 
    /// Пример:
    ///   await repo.DeleteMessageAsync(messageId);
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
 *            var mockRepo = new Mock<IMongoMessageRepository>();
 *            mockRepo.Setup(r => r.SendMessageAsync(It.IsAny<MongoMessage>()))
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
 *    builder.Services.AddScoped<IMongoMessageRepository, MongoMessageRepository>();
 *    
 *    // В контроллере
 *    public class MessagesController : ControllerBase
 *    {
 *        private readonly IMongoMessageRepository _repo;
 *        
 *        public MessagesController(IMongoMessageRepository repo)
 *        {
 *            _repo = repo;  // Автоматически инжектится!
 *        }
 *    }
 * 
 * 
 * 3. ЛЕГКО СМЕНИТЬ РЕАЛИЗАЦИЮ
 *    
 *    // Было: MongoDB
 *    builder.Services.AddScoped<IMongoMessageRepository, MongoMessageRepository>();
 *    
 *    // Стало: PostgreSQL
 *    builder.Services.AddScoped<IMongoMessageRepository, PostgresMessageRepository>();
 *    
 *    // Код контроллеров НЕ МЕНЯЕТСЯ!
 * 
 * ============================================================================
 */
