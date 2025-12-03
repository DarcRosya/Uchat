using System.Threading;
using System.Threading.Tasks;

namespace Uchat.Database.Services.Messaging;

public interface IMessageService
{
    Task<MessagingResult> SendMessageAsync(MessageCreateDto dto, CancellationToken cancellationToken = default);
    
    Task<MessagingResult> DeleteMessageAsync(string messageId, int userId, CancellationToken cancellationToken = default);
    
    Task<MessagingResult> EditMessageAsync(string messageId, int userId, string newContent, CancellationToken cancellationToken = default);

    Task<long> MarkMessagesAsReadUntilAsync(int chatId, int userId, DateTime untilTimestamp, CancellationToken cancellationToken = default);
}
