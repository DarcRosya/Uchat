namespace Uchat.Database.Entities;

public class ChatRoom
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CreatorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ChatRoomType Type { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public string? LastMessageContent { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public User Creator { get; set; } = null!;
    public ICollection<ChatRoomMember> Members { get; set; } = new List<ChatRoomMember>();
}


public enum ChatRoomType
{
    DirectMessage = 0,
    Public = 1,
    Private = 2,
}

