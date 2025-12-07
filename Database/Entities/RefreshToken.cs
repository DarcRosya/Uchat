namespace Uchat.Database.Entities;

public class RefreshToken
{
    public int Id { get; set; }
    
    /// (SHA256)
    public required string TokenHash { get; set; }
    public required int UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    
    /// 30 days maybe
    public DateTime ExpiresAt { get; set; }
    
    // Was the token revoked (manual logout)
    // true = token can no longer be used (even if it has not expired)
    public bool IsRevoked { get; set; }
    public User User { get; set; } = null!;
}
