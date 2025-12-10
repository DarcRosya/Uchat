using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Uchat.Shared.DTOs;

// ========================================================================
// REQUEST DTOs (Клиент → Сервер)
// ========================================================================

public class CreateChatRequestDto
{
    [Required(ErrorMessage = "Chat name is required")]
    [MinLength(1, ErrorMessage = "Name must be at least 1 character")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Chat type is required")]
    public string Type { get; set; } = "Private";

    [MaxLength(500)]
    public string? Description { get; set; }

    public string? IconUrl { get; set; }

    public IEnumerable<int>? InitialMemberIds { get; set; }

    public int? ParentChatRoomId { get; set; }

    public int? MaxMembers { get; set; }
}

public class AddMemberRequestDto
{
    // Меняем или добавляем поле для имени
    public string Username { get; set; } = string.Empty;
}

public class UpdateChatRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

// ========================================================================
// RESPONSE DTOs (Сервер → Клиент)
// ========================================================================

/// <summary>
/// Краткая информация о чате для списка чатов
/// </summary>
public class ChatRoomDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string Type { get; set; } = string.Empty;
    public int CreatorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MemberCount { get; set; }
    
    // Для списка чатов
    public string? LastMessageContent { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
}

/// <summary>
/// Детальная информация о чате (включая список участников)
/// </summary>
public class ChatRoomDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string Type { get; set; } = string.Empty;
    public int CreatorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ParentChatRoomId { get; set; }
    public int? MaxMembers { get; set; }
    public List<ChatMemberDto> Members { get; set; } = new();
}

/// <summary>
/// Информация об участнике чата
/// </summary>
public class ChatMemberDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}



