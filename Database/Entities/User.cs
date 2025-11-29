/*
 * ============================================================================
 * ENTITY MODEL: USER (Пользователь)
 * ============================================================================
 * 
 * В C# с Entity Framework Core мы делаем то же самое, но по-другому:
 * - Свойства (properties) = колонки в таблице
 * - Navigation properties = связи с другими таблицами (ForeignKey)
 * 
 * ============================================================================
 * ЗАЧЕМ НУЖНЫ НАВИГАЦИОННЫЕ СВОЙСТВА?
 * ============================================================================
 * 
 * В C# это делается через ICollection<T>:
 *   public ICollection<Message> SentMessages { get; set; }
 * 
 * Это позволяет делать:
 *   var user = await _context.Users.Include(u => u.SentMessages).FirstAsync();
 *   // Теперь user.SentMessages содержит все сообщения пользователя!
 * 
 * ============================================================================
 */

namespace Uchat.Database.Entities;

public class User
{
    // ========================================================================
    // ОСНОВНЫЕ ПОЛЯ (Primary Key и основная информация)
    // ========================================================================
    
    public int Id { get; set; }
    public required string Username { get; set; } = string.Empty;
    public required string Email { get; set; } = string.Empty;
    public required string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Хеш пароля (НЕ ХРАНИМ ПАРОЛЬ В ОТКРЫТОМ ВИДЕ!)
    /// Обычно используем SHA256, bcrypt или Argon2
    /// В БД: VARCHAR(256), NOT NULL
    /// 
    /// Пример создания хеша:
    ///   using System.Security.Cryptography;
    ///   var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password + salt));
    /// </summary>
    public required string PasswordHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Соль для хеширования пароля (случайная строка)
    /// Делает хеш уникальным даже для одинаковых паролей
    /// В БД: VARCHAR(128), NOT NULL
    /// 
    /// Пример генерации соли:
    ///   var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    /// </summary>
    public required string Salt { get; set; } = string.Empty;
    
    // ========================================================================
    // ДОПОЛНИТЕЛЬНАЯ ИНФОРМАЦИЯ О ПОЛЬЗОВАТЕЛЕ
    // ========================================================================

    public string? Bio { get ; set; }
    public string? AvatarUrl { get; set; }
    public DateTime? DateOfBirth { get; set; } // Correct format is [ DD.MM.YYYY ]

    // ========================================================================
    // ВРЕМЕННЫЕ МЕТКИ (Timestamps)
    // ========================================================================

    public DateTime CreatedAt { get; set; }

    // ========================================================================
    // СТАТУСЫ И ФЛАГИ
    // ========================================================================

    public UserStatus Status { get; set; } // (Online, Offline, Away, DoNotDisturb)
    public UserRole Role { get; set; } // (User, Admin) 
    public string LanguageCode { get; set; } = "en";
    public bool IsBlocked { get; set; } // Заблокирован ли пользователь (бан)
    public bool IsDeleted { get; set; } // Мягкое удаление (soft delete)
    
    // ========================================================================
    // НАВИГАЦИОННЫЕ СВОЙСТВА (Relationships / Foreign Keys)
    // ========================================================================
    // Это НЕ хранится в таблице Users! Это виртуальные связи с другими таблицами.
    // Entity Framework автоматически загружает эти данные когда нужно.
    // ========================================================================
    
    // ПРИМЕЧАНИЕ: Сообщения (Messages) хранятся в MongoDB!
    // Связь с сообщениями: LiteDbMessage.Sender.UserId == User.Id
    // Для получения сообщений пользователя используй MessageRepository
    
    /// <summary>
    /// Все групповые чаты, в которых участвует пользователь
    /// 
    /// Связь: User (Many) -> ChatRoomMember (Many) -> ChatRoom (Many)
    /// Это Many-to-Many связь через промежуточную таблицу ChatRoomMembers
    /// 
    /// Как использовать:
    ///   var user = await context.Users
    ///       .Include(u => u.ChatRoomMemberships)
    ///           .ThenInclude(m => m.ChatRoom)  // Загружаем сами чаты
    ///       .FirstAsync(u => u.Id == 1);
    ///   
    ///   foreach (var membership in user.ChatRoomMemberships) {
    ///       Console.WriteLine($"В чате: {membership.ChatRoom.Name}");
    ///       Console.WriteLine($"Роль: {membership.Role}");
    ///   }
    /// </summary>
    public ICollection<ChatRoomMember> ChatRoomMemberships { get; set; } = new List<ChatRoomMember>();
    
    /// <summary>
    /// Список контактов (друзей) этого пользователя
    /// 
    /// Связь: User (1) -> Contact (Many)
    /// Foreign Key в таблице Contacts: OwnerId -> Users.Id
    /// </summary>
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    
    /// <summary>
    /// Refresh tokens пользователя (активные сессии)
    /// 
    /// Связь: User (1) -> RefreshToken (Many)
    /// Foreign Key в таблице RefreshTokens: UserId -> Users.Id
    /// 
    /// Использование:
    ///   var user = await context.Users
    ///       .Include(u => u.RefreshTokens.Where(t => !t.IsRevoked))
    ///       .FirstAsync(u => u.Id == userId);
    ///   
    ///   Console.WriteLine($"Активных устройств: {user.RefreshTokens.Count}");
    /// </summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    
    // ========================================================================
    // НАВИГАЦИОННЫЕ СВОЙСТВА ДЛЯ ДРУЖБЫ
    // ========================================================================
    
    // /// Запросы дружбы, ОТПРАВЛЕННЫЕ этим пользователем
    // /// 
    // /// Как использовать:
    // ///   var user = await context.Users
    // ///       .Include(u => u.SentFriendshipRequests)
    // ///           .ThenInclude(f => f.Receiver)
    // ///       .FirstAsync(u => u.Id == 1);
    // ///   
    // ///   // Кому я отправил запросы?
    // ///   foreach (var request in user.SentFriendshipRequests.Where(f => f.Status == FriendshipStatus.Pending)) {
    // ///       Console.WriteLine($"Ждет ответа от: {request.Receiver.Username}");
    // ///   }
    // public ICollection<Friendship> SentFriendshipRequests { get; set; } = new List<Friendship>();
    
    /// Запросы дружбы, ПОЛУЧЕННЫЕ этим пользователем
    /// 
    /// Как использовать:
    ///   var user = await context.Users
    ///       .Include(u => u.ReceivedFriendshipRequests)
    ///           .ThenInclude(f => f.Sender)
    ///       .FirstAsync(u => u.Id == 1);
    ///   
    ///   // Кто хочет со мной подружиться?
    ///   foreach (var request in user.ReceivedFriendshipRequests.Where(f => f.Status == FriendshipStatus.Pending)) {
    ///       Console.WriteLine($"Запрос от: {request.Sender.Username}");
    ///   }
    public ICollection<Friendship> ReceivedFriendshipRequests { get; set; } = new List<Friendship>();
}

public enum UserStatus
{
    Offline = 0, // Пользователь оффлайн (не в сети)
    Online = 1, // Пользователь онлайн (активен)
    Away = 2, // Пользователь отошел (неактивен некоторое время)
    DoNotDisturb = 3 // Не беспокоить (получает уведомления, но показывается как занят)
}

public enum UserRole
{
    User = 0,
    Admin = 1,
}

public static class SupportedLanguages
{
    public const string English = "en";
    public const string Ukrainian = "uk";

    public static readonly string[] All = new[]
    {
        English, Ukrainian, 
    };
    
    public static bool IsSupported(string code)
    {
        return All.Contains(code);
    }
}