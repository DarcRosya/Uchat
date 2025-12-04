using System.ComponentModel.DataAnnotations;
using Uchat.Database.Entities;
using Uchat.Shared.DTOs;

namespace Uchat.Server.DTOs;

// ========================================================================
// EXTENSION METHODS (Маппинг Entity -> DTO)
// ========================================================================

public static class ChatRoomMappingExtensions
{
    /// <summary>
    /// Конвертирует ChatRoom в краткий DTO
    /// </summary>
    public static ChatRoomDto ToDto(this ChatRoom chatRoom)
    {
        return new ChatRoomDto
        {
            Id = chatRoom.Id,
            Name = chatRoom.Name,
            Description = chatRoom.Description,
            IconUrl = chatRoom.IconUrl,
            Type = chatRoom.Type.ToString(),
            CreatorId = chatRoom.CreatorId,
            CreatedAt = chatRoom.CreatedAt,
            MemberCount = chatRoom.Members?.Count ?? 0
        };
    }

    /// <summary>
    /// Конвертирует ChatRoom в детальный DTO
    /// </summary>
    public static ChatRoomDetailDto ToDetailDto(this ChatRoom chatRoom)
    {
        return new ChatRoomDetailDto
        {
            Id = chatRoom.Id,
            Name = chatRoom.Name,
            Description = chatRoom.Description,
            IconUrl = chatRoom.IconUrl,
            Type = chatRoom.Type.ToString(),
            CreatorId = chatRoom.CreatorId,
            CreatedAt = chatRoom.CreatedAt,
            ParentChatRoomId = null,
            MaxMembers = chatRoom.MaxMembers,
            Members = chatRoom.Members?.Select(m => new ChatMemberDto
            {
                UserId = m.UserId,
                Username = m.User?.Username ?? "Unknown",
                Role = "Member", // TODO: добавить роли
                JoinedAt = m.JoinedAt
            }).ToList() ?? new List<ChatMemberDto>()
        };
    }
}
