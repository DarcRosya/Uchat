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
        using var msg = new MailMessage();
        msg.From = new MailAddress(_settings.FromEmail, _settings.FromName);
        msg.To.Add(new MailAddress(toEmail));
        msg.Subject = subject;
        msg.Body = htmlBody;
        msg.IsBodyHtml = true;

        using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort);
        client.EnableSsl = _settings.EnableSsl;

        if (!string.IsNullOrEmpty(_settings.Username))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
        }

        // SmtpClient doesn't have an async SendMailMessage in older frameworks,
        // but SendMailAsync exists on modern runtimes.
        await client.SendMailAsync(msg);
    }
}
