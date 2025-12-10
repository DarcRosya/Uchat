/*
 * ============================================================================
 * ENTITY MODEL: USER (Пользователь)
 * ============================================================================
 */

namespace Uchat.Database.Entities;

public class User
{
    public int Id { get; set; }

    public required string Username { get; set; } = string.Empty;
    public required string Email { get; set; } = string.Empty;
    public required string DisplayName { get; set; } = string.Empty;
    public required string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public string? PasswordResetCode { get; set; }
    public DateTime? PasswordResetCodeExpiresAt { get; set; }

    public bool EmailConfirmed { get; set; } = false; 
    
    public ICollection<ChatRoomMember> ChatRoomMemberships { get; set; } = new List<ChatRoomMember>();
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<UserSecurityToken> SecurityTokens { get; set; } = new List<UserSecurityToken>();
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
