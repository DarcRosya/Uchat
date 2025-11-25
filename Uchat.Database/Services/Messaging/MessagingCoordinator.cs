using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Uchat.Database.Context;
using Uchat.Database.Entities;
using Uchat.Database.LiteDB;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Database.Services.Messaging;

/// <summary>
/// Orchestrates the flow that touches both SQLite metadata and LiteDB messages.
/// </summary>
public sealed class MessagingCoordinator : IMessagingCoordinator
{
    private readonly UchatDbContext _context;
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger<MessagingCoordinator> _logger;

    public MessagingCoordinator(
        UchatDbContext context,
        IMessageRepository messageRepository,
        ILogger<MessagingCoordinator> logger)
    {
        _context = context;
        _messageRepository = messageRepository;
        _logger = logger;
    }

    public async Task<MessagingResult> SendMessageAsync(MessageCreateDto dto, CancellationToken cancellationToken = default)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        LiteDbMessage? liteMessage = null;
        string? messageId = null;

        try
        {
            var chatRoom = await _context.ChatRooms
                .Include(cr => cr.Members)
                .FirstOrDefaultAsync(cr => cr.Id == dto.ChatRoomId, cancellationToken);

            if (chatRoom == null)
            {
                return MessagingResult.Failure($"ChatRoom {dto.ChatRoomId} not found.");
            }

            if (!chatRoom.Members.Any(m => m.UserId == dto.SenderId))
            {
                return MessagingResult.Failure("Sender is not a member of the chat.");
            }

            var sender = await _context.Users.FindAsync(new object[] { dto.SenderId }, cancellationToken: cancellationToken);
            if (sender == null)
            {
                return MessagingResult.Failure($"Sender {dto.SenderId} not found.");
            }

            liteMessage = BuildLiteDbMessage(dto, sender);
            messageId = await _messageRepository.SendMessageAsync(liteMessage);

            chatRoom.LastActivityAt = liteMessage.SentAt;
            chatRoom.TotalMessagesCount++;

            await UpdateContactStatsAsync(chatRoom.Members.Select(m => m.UserId), dto.SenderId, liteMessage.SentAt, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return MessagingResult.SuccessResult(messageId, liteMessage.SentAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MessagingCoordinator failed for chat {ChatId}", dto.ChatRoomId);
            await transaction.RollbackAsync(cancellationToken);

            if (!string.IsNullOrEmpty(messageId))
            {
                await TryDeleteMessageAsync(messageId);
            }

            return MessagingResult.Failure(ex.Message, needsReconciliation: !string.IsNullOrEmpty(messageId));
        }
    }

    private static LiteDbMessage BuildLiteDbMessage(MessageCreateDto dto, User sender)
    {
        return new LiteDbMessage
        {
            ChatId = dto.ChatRoomId,
            Sender = new MessageSender
            {
                UserId = sender.Id,
                Username = sender.Username,
                DisplayName = sender.DisplayName,
                AvatarUrl = sender.AvatarUrl
            },
            Content = dto.Content ?? string.Empty,
            Type = string.IsNullOrWhiteSpace(dto.Type) ? "text" : dto.Type,
            Attachments = dto.Attachments != null
                ? dto.Attachments.ToList()
                : new List<MessageAttachment>(),
            ReplyToMessageId = dto.ReplyToMessageId,
            SentAt = DateTime.UtcNow
        };
    }

    private async Task UpdateContactStatsAsync(IEnumerable<int> participantIds, int senderId, DateTime sentAt, CancellationToken cancellationToken)
    {
        var distinctParticipants = participantIds
            .Where(id => id != senderId)
            .Distinct()
            .ToList();

        foreach (var participantId in distinctParticipants)
        {
            await UpdateContactAsync(senderId, participantId, sentAt, cancellationToken);
            await UpdateContactAsync(participantId, senderId, sentAt, cancellationToken);
        }
    }

    private async Task UpdateContactAsync(int ownerId, int contactUserId, DateTime sentAt, CancellationToken cancellationToken)
    {
        var contact = await _context.Contacts
            .FirstOrDefaultAsync(c => c.OwnerId == ownerId && c.ContactUserId == contactUserId,
                cancellationToken);

        if (contact == null)
        {
            return;
        }

        contact.LastMessageAt = sentAt;
        contact.MessageCount++;
    }

    private async Task TryDeleteMessageAsync(string id)
    {
        try
        {
            await _messageRepository.DeleteMessageAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete orphaned message {MessageId}", id);
        }
    }
}
