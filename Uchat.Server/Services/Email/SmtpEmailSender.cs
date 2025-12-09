using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Uchat.Server.Services.Email;

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;

    public SmtpEmailSender(IOptions<EmailSettings> options)
    {
        _settings = options.Value;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrEmpty(_settings.SmtpHost) || string.IsNullOrEmpty(_settings.Username))
        {
            throw new Exception("SMTP settings are missing in appsettings.json");
        }

        using var client = new SmtpClient(); 

        client.Host = _settings.SmtpHost;
        client.Port = _settings.SmtpPort;
        client.EnableSsl = _settings.EnableSsl;
        
        client.UseDefaultCredentials = false;
        client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
        
        using var msg = new MailMessage();
        msg.From = new MailAddress(_settings.FromEmail, _settings.FromName);
        msg.To.Add(new MailAddress(toEmail));
        msg.Subject = subject;
        msg.Body = htmlBody;
        msg.IsBodyHtml = true;

        await client.SendMailAsync(msg);
    }
}
