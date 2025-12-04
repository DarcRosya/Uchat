/*
 * ============================================================================
 * ENTITY MODEL: USER (Пользователь)
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
    public required string PasswordHash { get; set; } = string.Empty;
    
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

    public bool EmailConfirmed { get; set; } = false; 
    public UserRole Role { get; set; } = UserRole.User; // (User, Admin) 
    public string LanguageCode { get; set; } = "en";
    public bool IsDeleted { get; set; } = false; // Мягкое удаление (soft delete)
    
    // ========================================================================
    // НАВИГАЦИОННЫЕ СВОЙСТВА (Relationships / Foreign Keys)
    // ========================================================================
    
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
    /// </summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<UserSecurityToken> SecurityTokens { get; set; } = new List<UserSecurityToken>();
    public ICollection<Friendship> ReceivedFriendshipRequests { get; set; } = new List<Friendship>();
}

public class UserSecurityToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required User User { get; set; }
    
    public TokenType Type { get; set; } // EmailConfirmation, PasswordReset
    public required string Token { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;
}

public enum TokenType
{
    EmailConfirmation = 1,
    PasswordReset = 2
}

// public enum UserStatus
// {
//     Offline = 0, // Пользователь оффлайн (не в сети)
//     Online = 1, // Пользователь онлайн (активен)
//     Away = 2, // Пользователь отошел (неактивен некоторое время)
//     DoNotDisturb = 3 // Не беспокоить (получает уведомления, но показывается как занят)
// }

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