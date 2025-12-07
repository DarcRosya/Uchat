using System.Threading;
using System.Threading.Tasks;
using Uchat.Shared.DTOs;
using Uchat.Server.DTOs;

namespace Uchat.Server.Services.Messaging;

public interface IMessageService
{
    Task<MessagingResult> SendMessageAsync(MessageCreateDto dto, CancellationToken cancellationToken = default);

    Task<PaginatedMessagesDto> GetMessagesAsync(int chatId, int userId, int limit = 50, DateTime? before = null);

    Task<Dictionary<int, MessageDto>> GetLastMessagesForChatsBatch(Dictionary<int, DateTime?> chatsWithClearDates);
    
    Task<MessageDto?> GetMessageByIdAsync(string messageId);
    
    Task<bool> MessageExistsAsync(string messageId, int chatId);
    
    Task<MessagingResult> DeleteMessageAsync(string messageId, int userId, CancellationToken cancellationToken = default);
    
    Task<MessagingResult> EditMessageAsync(string messageId, int userId, string newContent, CancellationToken cancellationToken = default);

    Task<long> MarkMessagesAsReadUntilAsync(int chatId, int userId, DateTime untilTimestamp, CancellationToken cancellationToken = default);
}
