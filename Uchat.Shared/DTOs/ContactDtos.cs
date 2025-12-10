namespace Uchat.Shared.DTOs;

public class ContactDto
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public int ContactUserId { get; set; }
    public string ContactUsername { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public ContactStatusDto Status { get; set; }
    public int? SavedChatRoomId { get; set; }
    public DateTime AddedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessageContent { get; set; }
    public int UnreadCount { get; set; }
}

public enum ContactStatusDto
{
    None = 0,
    Friend = 1,
    RequestSent = 2,
    RequestReceived = 3
}

public class SendFriendRequestDto
{
    public string Username { get; set; } = string.Empty;
}
