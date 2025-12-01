using System.Threading;
using System.Threading.Tasks;

namespace Database.Services.Messaging;

/// <summary>
/// Service for managing chat messages across SQLite (metadata) and LiteDB (content).
/// Handles validation, permissions, and transactional consistency.
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// Sends a new message to a chat room.
    /// Validates sender permissions, chat membership, and message content.
    /// </summary>
    Task<MessagingResult> SendMessageAsync(MessageCreateDto dto, CancellationToken cancellationToken = default);
}
