/*
 * ============================================================================
 * REPOSITORY PATTERN (Паттерн Репозиторий)
 * ============================================================================
 * 
 * ЧТО ТАКОЕ REPOSITORY?
 * Это паттерн проектирования, который:
 * 1. Абстрагирует работу с базой данных
 * 2. Предоставляет чистый API для CRUD операций
 * 3. Отделяет бизнес-логику от логики доступа к данным
 * 
 * В FastAPI ты работал напрямую с сессией:
 *   db.query(User).filter(User.id == user_id).first()
 * 
 * С Repository паттерном:
 *   await userRepository.GetByIdAsync(userId)
 * 
 * ЗАЧЕМ ЭТО НУЖНО?
 * - Код чище и понятнее
 * - Легко тестировать (можно замокать репозиторий)
 * - Если поменяешь БД (SQLite -> PostgreSQL), меняешь только репозиторий
 * - Переиспользование кода (один метод GetByIdAsync везде)
 * 
 * ============================================================================
 */

using Uchat.Database.Entities;

namespace Uchat.Database.Repositories.Interfaces;

/// <summary>
/// Интерфейс репозитория для работы с пользователями
/// 
/// ЗАЧЕМ ИНТЕРФЕЙС?
/// - Dependency Injection (можем подставить мок для тестов)
/// - Контракт: "любой репозиторий юзеров ДОЛЖЕН иметь эти методы"
/// - Можем сделать несколько реализаций (SqliteUserRepository, PostgresUserRepository)
/// 
/// В FastAPI ты скорее всего не использовал интерфейсы.
/// В C# это стандартная практика!
/// </summary>
public interface IUserRepository
{
    // ========================================================================
    // CREATE (Создание)
    // ========================================================================
    
    /// <summary>
    /// Создать нового пользователя в базе данных
    /// 
    /// SQL аналог:
    ///   INSERT INTO Users (Username, PasswordHash, ...) 
    ///   VALUES ('john', 'hash123', ...)
    /// 
    /// Использование:
    ///   var user = new User { Username = "john", ... };
    ///   var created = await userRepo.CreateAsync(user);
    ///   // created.Id теперь заполнен (автоинкремент)
    /// </summary>
    /// <param name="user">Объект пользователя для создания</param>
    /// <returns>Созданный пользователь с заполненным Id</returns>
    Task<User> CreateAsync(User user);
    
    // ========================================================================
    // READ (Чтение)
    // ========================================================================
    
    /// <summary>
    /// Получить пользователя по ID
    /// 
    /// SQL: SELECT * FROM Users WHERE Id = @id
    /// 
    /// Возвращает NULL если не найден
    /// </summary>
    Task<User?> GetByIdAsync(int id);
    
    /// <summary>
    /// Получить пользователя по имени (для логина)
    /// 
    /// SQL: SELECT * FROM Users WHERE Username = @username
    /// 
    /// Использует индекс IX_Users_Username для быстрого поиска!
    /// </summary>
    Task<User?> GetByUsernameAsync(string username);
    
    /// <summary>
    /// Получить пользователя по email
    /// 
    /// SQL: SELECT * FROM Users WHERE Email = @email
    /// </summary>
    Task<User?> GetByEmailAsync(string email);
    
    /// <summary>
    /// Получить ВСЕХ пользователей
    /// 
    /// SQL: SELECT * FROM Users ORDER BY Username
    /// 
    /// ВНИМАНИЕ: Если пользователей 1 миллион, вернет ВСЕ!
    /// В production лучше использовать пагинацию:
    ///   Task&lt;IEnumerable&lt;User&gt;&gt; GetAllAsync(int page = 1, int pageSize = 50);
    /// </summary>
    Task<IEnumerable<User>> GetAllAsync();
    
    /// <summary>
    /// Поиск пользователей по части имени
    /// 
    /// SQL: SELECT * FROM Users 
    ///      WHERE Username LIKE '%searchTerm%' 
    ///         OR DisplayName LIKE '%searchTerm%'
    ///      LIMIT @limit
    /// 
    /// Использование:
    ///   var users = await userRepo.SearchByUsernameAsync("john", limit: 10);
    ///   // Найдет: "john", "johnny", "john_doe", "big_john"
    /// </summary>
    /// <param name="searchTerm">Часть имени для поиска</param>
    /// <param name="limit">Максимальное количество результатов (по умолчанию 20)</param>
    Task<IEnumerable<User>> SearchByUsernameAsync(string searchTerm, int limit = 20);
    
    /// <summary>
    /// Проверить существование пользователя по имени
    /// 
    /// SQL: SELECT EXISTS(SELECT 1 FROM Users WHERE Username = @username)
    /// 
    /// Быстрее чем GetByUsernameAsync, если нужна только проверка!
    /// 
    /// Использование при регистрации:
    ///   if (await userRepo.ExistsAsync(username))
    ///       return "Username уже занят";
    /// </summary>
    Task<bool> ExistsAsync(string username);
    
    // ========================================================================
    // UPDATE (Обновление)
    // ========================================================================
    
    /// <summary>
    /// Обновить пользователя в БД
    /// 
    /// SQL: UPDATE Users SET ... WHERE Id = @id
    /// 
    /// EF Core автоматически определит какие поля изменились!
    /// 
    /// Использование:
    ///   var user = await userRepo.GetByIdAsync(1);
    ///   user.DisplayName = "New Name";
    ///   await userRepo.UpdateAsync(user);
    /// </summary>
    /// <returns>true если обновление успешно, false если пользователь не найден</returns>
    Task<bool> UpdateAsync(User user);
    
    /// <summary>
    /// Обновить только статус пользователя (оптимизировано)
    /// 
    /// SQL: UPDATE Users SET Status = @status, LastSeenAt = @now WHERE Id = @userId
    /// 
    /// Быстрее чем UpdateAsync(user) если меняем только одно поле!
    /// </summary>
    Task<bool> UpdateStatusAsync(int userId, UserStatus status);
    
    /// <summary>
    /// Обновить время последней активности
    /// 
    /// SQL: UPDATE Users SET LastSeenAt = @lastSeen WHERE Id = @userId
    /// 
    /// Вызывается при каждой активности пользователя
    /// </summary>
    Task<bool> UpdateLastSeenAsync(int userId, DateTime lastSeen);
    
    /// <summary>
    /// Обновить пароль пользователя
    /// 
    /// SQL: UPDATE Users SET PasswordHash = @hash, Salt = @salt WHERE Id = @userId
    /// 
    /// Использование (смена пароля):
    ///   var salt = GenerateNewSalt();
    ///   var hash = HashPassword(newPassword, salt);
    ///   await userRepo.UpdatePasswordAsync(userId, hash, salt);
    /// </summary>
    Task<bool> UpdatePasswordAsync(int userId, string passwordHash, string salt);
    
    // ========================================================================
    // DELETE (Удаление)
    // ========================================================================
    
    /// <summary>
    /// ЖЕСТКОЕ удаление пользователя из БД
    /// 
    /// SQL: DELETE FROM Users WHERE Id = @userId
    /// 
    /// ВНИМАНИЕ: Удалится навсегда!
    /// Может не сработать если:
    /// - У пользователя есть сообщения (DeleteBehavior.Restrict)
    /// - Нарушает другие foreign key constraints
    /// 
    /// В production лучше использовать Soft Delete (IsDeleted = true)
    /// </summary>
    Task<bool> DeleteAsync(int userId);
    
    /// <summary>
    /// Заблокировать/разблокировать пользователя
    /// 
    /// SQL: UPDATE Users SET IsBlocked = @isBlocked WHERE Id = @userId
    /// 
    /// Заблокированный пользователь:
    /// - Не может войти в систему
    /// - Не может отправлять сообщения
    /// - Аккаунт сохраняется (не удаляется)
    /// </summary>
    Task<bool> BlockUserAsync(int userId, bool isBlocked);
    
    // ========================================================================
    // ДОПОЛНИТЕЛЬНЫЕ МЕТОДЫ (Helper methods)
    // ========================================================================
    
    /// <summary>
    /// Получить количество онлайн пользователей
    /// 
    /// SQL: SELECT COUNT(*) FROM Users WHERE Status = 1
    /// 
    /// Для статистики на главной странице
    /// </summary>
    Task<int> GetOnlineCountAsync();
    
    /// <summary>
    /// Получить список онлайн пользователей
    /// 
    /// SQL: SELECT * FROM Users WHERE Status = 1 ORDER BY Username
    /// 
    /// Для отображения "Кто сейчас онлайн"
    /// </summary>
    Task<IEnumerable<User>> GetOnlineUsersAsync();
}

/*
 * ============================================================================
 * ЗАДАНИЕ ДЛЯ ТЕБЯ (ПРАКТИКА):
 * ============================================================================
 * 
 * Создай интерфейсы для ОСТАЛЬНЫХ репозиториев:
 * 
 * 1. IMessageRepository - для работы с сообщениями
 *    Методы:
 *    - CreateAsync(Message message)
 *    - GetByIdAsync(int id)
 *    - GetDirectMessagesAsync(int userId1, int userId2, int limit = 100)
 *    - GetChatRoomMessagesAsync(int chatRoomId, int limit = 100)
 *    - EditMessageAsync(int messageId, string newContent)
 *    - DeleteMessageAsync(int messageId) // Soft delete (IsDeleted = true)
 *    - MarkAsReadAsync(int messageId)
 *    - GetUnreadCountAsync(int userId)
 * 
 * 2. IChatRoomRepository - для работы с групповыми чатами
 *    Методы:
 *    - CreateAsync(ChatRoom chatRoom)
 *    - GetByIdAsync(int id)
 *    - GetUserChatRoomsAsync(int userId) // Все группы пользователя
 *    - AddMemberAsync(ChatRoomMember member)
 *    - RemoveMemberAsync(int chatRoomId, int userId)
 *    - UpdateMemberRoleAsync(int chatRoomId, int userId, ChatRoomRole role)
 * 
 * 3. IContactRepository - для работы с контактами
 *    Методы:
 *    - AddContactAsync(int ownerId, int contactUserId)
 *    - RemoveContactAsync(int ownerId, int contactUserId)
 *    - GetUserContactsAsync(int userId)
 *    - BlockContactAsync(int contactId, bool isBlocked)
 *    - SetFavoriteAsync(int contactId, bool isFavorite)
 * 
 * Создай файлы:
 * - Repositories/Interfaces/IMessageRepository.cs
 * - Repositories/Interfaces/IChatRoomRepository.cs
 * - Repositories/Interfaces/IContactRepository.cs
 * 
 * Затем создай РЕАЛИЗАЦИИ этих интерфейсов:
 * - Repositories/Implementations/MessageRepository.cs
 * - Repositories/Implementations/ChatRoomRepository.cs
 * - Repositories/Implementations/ContactRepository.cs
 * 
 * Посмотри на UserRepository.cs как пример реализации!
 * 
 * ============================================================================
 * ПОДСКАЗКИ:
 * ============================================================================
 * 
 * Для GetDirectMessagesAsync используй:
 *   return await _context.Messages
 *       .Where(m => (m.SenderId == userId1 && m.ReceiverId == userId2) ||
 *                   (m.SenderId == userId2 && m.ReceiverId == userId1))
 *       .OrderByDescending(m => m.SentAt)
 *       .Take(limit)
 *       .ToListAsync();
 * 
 * Для GetUserChatRoomsAsync:
 *   return await _context.ChatRoomMembers
 *       .Include(m => m.ChatRoom)
 *       .Where(m => m.UserId == userId && m.LeftAt == null)
 *       .Select(m => m.ChatRoom)
 *       .ToListAsync();
 * 
 * ============================================================================
 */
