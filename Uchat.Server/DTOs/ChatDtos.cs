using System.ComponentModel.DataAnnotations;
using Uchat.Database.Entities;
using Uchat.Shared.DTOs;

namespace Uchat.Server.DTOs;

public static class ChatRoomMappingExtensions
{
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
                JoinedAt = m.JoinedAt
            }).ToList() ?? new List<ChatMemberDto>()
        };
    }
}
