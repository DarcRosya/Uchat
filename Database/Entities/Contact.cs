using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Uchat.Database.Entities;

public class Contact
{
    public int Id { get; set; }

    public int OwnerId { get; set; }
    public int ContactUserId { get; set; }

    public ContactStatus Status { get; set; } = ContactStatus.None;
    public DateTime? FriendRequestRejectedAt { get; set; }

    // ID of a private chat with this user (if created).
    // Allows you to instantly open a chat without searching the ChatRooms table.
    public int? SavedChatRoomId { get; set; }

    public string? Nickname { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastMessageAt { get; set; }
    public int MessageCount { get; set; }

    public User Owner { get; set; } = null!;
    public User ContactUser { get; set; } = null!;
    public ChatRoom? SavedChatRoom { get; set; }
}

public enum ContactStatus
{
    None = 0,             
    Friend = 1,           
    RequestSent = 2,      
    RequestReceived = 3     
}