using System;
using System.Collections.Generic;

namespace Uchat.Shared.DTOs;

public sealed class MessageCreateDto
{
    public int ChatRoomId { get; set; }
    public int SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public string? ReplyToMessageId { get; set; }
}

public sealed class MessageDto
{
    public string Id { get; set; } = string.Empty;
    public int ChatRoomId { get; set; }
    public MessageSenderDto Sender { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public string? ReplyToMessageId { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
    
    public MessageReplyDto? ReplyTo { get; set; }
}

public class MessageSenderDto 
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public class MessageReplyDto
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
}


public class PaginationInfo
{
    public DateTime? NextCursor { get; set; }
    public bool HasMore { get; set; }
    public int Count { get; set; }
}

public class PaginatedMessagesDto
{
    public List<MessageDto> Messages { get; set; } = new();
    public PaginationInfo Pagination { get; set; } = new();
}
