namespace Uchat.Database.Entities;

public class PendingRegistration
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    
    public string? Code { get; set; }
    public DateTime? CodeExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}