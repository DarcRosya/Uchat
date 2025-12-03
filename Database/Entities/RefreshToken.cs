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
    
    /// Был ли токен отозван (вручную logout)
    /// true = токен больше нельзя использовать (даже если не истек)
    public bool IsRevoked { get; set; }
    public User User { get; set; } = null!;
}
