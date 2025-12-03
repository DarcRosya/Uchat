namespace Uchat.Database.Entities;

public class ChatRoom
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }

    // ========================================================================
    // СОЗДАТЕЛЬ И МЕТАДАННЫЕ
    // ========================================================================

    public int? ParentChatRoomId { get; set; }
    public ChatRoom? ParentChatRoom { get; set; }
    public int CreatorId { get; set; }
    public DateTime CreatedAt { get; set; }

    // ========================================================================
    // НАСТРОЙКИ ГРУППЫ
    // ========================================================================

    public ChatRoomType Type { get; set; }
    public int? MaxMembers { get; set; }

    // ========================================================================
    // PERMISSIONS
    // ========================================================================

    public bool? DefaultCanSendMessages { get; set; }
    public bool? DefaultCanSendPhotos { get; set; }
    public bool? DefaultCanSendVideos { get; set; }
    public bool? DefaultCanSendStickers { get; set; }
    public bool? DefaultCanSendMusic { get; set; }
    public bool? DefaultCanSendFiles { get; set; }
    public bool? DefaultCanInviteUsers { get; set; }
    public bool? DefaultCanPinMessages { get; set; }
    public bool? DefaultCanCustomizeGroup { get; set; }
    public int? SlowModeSeconds { get; set; }

    // ========================================================================
    // СТАТИСТИКА
    // ========================================================================

    public int TotalMessagesCount { get; set; }
    public DateTime? LastActivityAt { get; set; }

    // ========================================================================
    // NAVIGATION PROPERTIES
    // ========================================================================

    public User Creator { get; set; } = null!;
    public ICollection<ChatRoomMember> Members { get; set; } = new List<ChatRoomMember>();
    public ICollection<ChatRoom> Topics { get; set; } = new List<ChatRoom>();
}


public enum ChatRoomType
{
    DirectMessage = 0,
    Public = 1,
    Private = 2,
    Topic = 3,
    Channel = 4
}

