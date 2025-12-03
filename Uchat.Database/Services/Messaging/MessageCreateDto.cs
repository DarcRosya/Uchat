using System.Collections.Generic;
using Uchat.Database.LiteDB;

namespace Uchat.Database.Services.Messaging;

/// <summary>
/// DTO for creating a new chat message through the MessageService.
/// </summary>
public sealed class MessageCreateDto
{
    public int ChatRoomId { get; set; }
    public int SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public string? ReplyToMessageId { get; set; }
    public List<MessageAttachment> Attachments { get; set; } = new();
}
