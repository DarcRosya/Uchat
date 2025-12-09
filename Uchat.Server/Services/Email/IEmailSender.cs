using System.Threading.Tasks;

namespace Uchat.Server.Services.Email;

public interface IEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
}
