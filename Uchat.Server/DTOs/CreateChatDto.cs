using System;
using System.Collections.Generic;
using Uchat.Database.Entities;

namespace Uchat.Database.Services.Chat;

public sealed class CreateChatDto
{
    public int CreatorId { get; init; }
    public string Name { get; init; } = string.Empty;
    public ChatRoomType Type { get; init; } = ChatRoomType.Private;
    public string? Description { get; init; }
    public string? IconUrl { get; init; }
    public IEnumerable<int> InitialMemberIds { get; init; } = Array.Empty<int>();
    public int? ParentChatRoomId { get; init; }
    public int? MaxMembers { get; init; }
}
