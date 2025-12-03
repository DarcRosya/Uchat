using System.Threading;
using System.Threading.Tasks;

namespace Uchat.Database.Services.Messaging;

/// <summary>
/// Service for managing chat messages across SQLite (metadata) and MongoDB (content).
/// Handles validation, permissions, and transactional consistency.
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// Sends a new message to a chat room.
    /// Validates sender permissions, chat membership, and message content.
    /// </summary>
    Task<MessagingResult> SendMessageAsync(MessageCreateDto dto, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a message (soft delete).
    /// Validates that the user has permission to delete the message.
    /// Only the message author or chat admins with CanDeleteMessages permission can delete.
    /// </summary>
    /// <param name="messageId">ID of the message to delete</param>
    /// <param name="userId">ID of the user attempting to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with success status and error message if failed</returns>
    Task<MessagingResult> DeleteMessageAsync(string messageId, int userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Edits a message content.
    /// Validates that the user has permission to edit the message.
    /// Only the message author can edit their messages.
    /// </summary>
    /// <param name="messageId">ID of the message to edit</param>
    /// <param name="userId">ID of the user attempting to edit</param>
    /// <param name="newContent">New message content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with success status and error message if failed</returns>
    Task<MessagingResult> EditMessageAsync(string messageId, int userId, string newContent, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks messages as read until specified timestamp.
    /// This is the optimal approach for marking multiple messages as read.
    /// </summary>
    /// <param name="chatId">ID of the chat room</param>
    /// <param name="userId">ID of the user</param>
    /// <param name="untilTimestamp">Mark all messages up to this time as read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of messages marked as read</returns>
    Task<long> MarkMessagesAsReadUntilAsync(int chatId, int userId, DateTime untilTimestamp, CancellationToken cancellationToken = default);
}
