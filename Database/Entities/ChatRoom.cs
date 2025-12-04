namespace Uchat.Database.Entities;

public class ChatRoom
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public int CreatorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ChatRoomType Type { get; set; }
    public int? MaxMembers { get; set; }
    public int TotalMessagesCount { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public User Creator { get; set; } = null!;
    public ICollection<ChatRoomMember> Members { get; set; } = new List<ChatRoomMember>();
}


public enum ChatRoomType
{
    DirectMessage = 0,
    Public = 1,
    Private = 2,
}

