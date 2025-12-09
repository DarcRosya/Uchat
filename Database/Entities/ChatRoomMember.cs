namespace Uchat.Database.Entities;

public class ChatRoomMember
{
    public int Id { get; set; }

    public int ChatRoomId { get; set; }
    public int UserId { get; set; }

    public DateTime JoinedAt { get; set; }
    public DateTime? ClearedHistoryAt { get; set; }

    public bool IsPending { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    public int? InvitedById { get; set; }

    public ChatRoom ChatRoom { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? InvitedBy { get; set; }
}