namespace Uchat.Server.Services.Email;

public class EmailSettings
{
    public string SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 25;
    public bool EnableSsl { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromEmail { get; set; } = "noreply@uchat.local";
    public string FromName { get; set; } = "Uchat";
}
