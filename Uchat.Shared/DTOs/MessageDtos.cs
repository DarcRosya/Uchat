using System;
using System.Collections.Generic;

namespace Uchat.Shared.DTOs;

// ========================================================================
// REQUEST DTOs
// ========================================================================

/// <summary>
/// DTO для создания нового сообщения
/// </summary>
public sealed class MessageCreateDto
{
    public int ChatRoomId { get; set; }
    public int SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public string? ReplyToMessageId { get; set; }
}

// ========================================================================
// RESPONSE DTOs
// ========================================================================

/// <summary>
/// DTO для сообщения чата
/// </summary>
public sealed class MessageDto
{
    public string Id { get; set; } = string.Empty;
    public int ChatRoomId { get; set; }
    public MessageSenderDto Sender { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public string? ReplyToMessageId { get; set; }
    public List<MessageAttachment> Attachments { get; set; } = new();
    public Dictionary<string, int> ReactionsCount { get; set; } = new();
    public List<string> MyReactions { get; set; } = new();
    public DateTime SentAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
    
    /// <summary>
    /// Информация о сообщении, на которое отвечают (если это ответ)
    /// </summary>
    public MessageReplyDto? ReplyTo { get; set; }
}

public class MessageAttachment
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty; // image/jpeg, video/mp4, etc.
    public long FileSize { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; } // Для превью картинок/видео
    public int? Width { get; set; }  // Для изображений
    public int? Height { get; set; }
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

/// <summary>
/// Метаданные пагинации для cursor-based навигации
/// </summary>
public class PaginationInfo
{
    /// <summary>
    /// Cursor для загрузки следующей порции (старых сообщений)
    /// Это timestamp последнего сообщения в текущем списке
    /// </summary>
    public DateTime? NextCursor { get; set; }
    
    /// <summary>
    /// Есть ли ещё сообщения для загрузки
    /// </summary>
    public bool HasMore { get; set; }
    
    /// <summary>
    /// Количество сообщений в текущем ответе
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
/// Ответ API со списком сообщений и информацией о пагинации
/// </summary>
public class PaginatedMessagesDto
{
    public List<MessageDto> Messages { get; set; } = new();
    public PaginationInfo Pagination { get; set; } = new();
}
