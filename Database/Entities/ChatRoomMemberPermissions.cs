namespace Uchat.Database.Entities;

/// Three-level permission system:
/// - GLOBAL (ChatRoom) - default for all members
/// - ADMIN (this table for admins) - what admins can do
/// - EXCEPTIONS (this table for members) - individual overrides
/// </summary>
public class ChatRoomMemberPermissions
{
    public int Id { get; set; }
    public int ChatRoomMemberId { get; set; }
    
    // ================================================================
    // MESSAGE PERMISSIONS
    // ================================================================
    
    /// Can send text messages
    /// NULL = inherit from ChatRoom.DefaultCanSendMessages
    /// For admins: usually true
    /// For members: exception override
    public bool? CanSendMessages { get; set; }
    
    /// Can send photos
    /// NULL = inherit from ChatRoom.DefaultCanSendPhotos
    public bool? CanSendPhotos { get; set; }
    
    /// Can send videos
    /// NULL = inherit from ChatRoom.DefaultCanSendVideos
    public bool? CanSendVideos { get; set; }
    
    /// Can send stickers and GIFs
    /// NULL = inherit from ChatRoom.DefaultCanSendStickers
    public bool? CanSendStickers { get; set; }
    
    /// Can send music/audio files
    /// NULL = inherit from ChatRoom.DefaultCanSendMusic
    public bool? CanSendMusic { get; set; }
    
    /// Can send files and documents
    /// NULL = inherit from ChatRoom.DefaultCanSendFiles
    public bool? CanSendFiles { get; set; }
    
    /// Can delete ANY messages (admin only)
    /// NULL = false for regular members
    public bool? CanDeleteMessages { get; set; }
    
    /// Can pin messages
    /// NULL = false for regular members
    public bool? CanPinMessages { get; set; }
    
    // ================================================================
    // MEMBER MANAGEMENT (usually admin-only)
    // ================================================================
    
    /// Can invite new users
    /// NULL = inherit from ChatRoom.DefaultCanInviteUsers
    public bool? CanInviteUsers { get; set; }
    
    /// Can remove/kick users (admin only)
    public bool? CanRemoveUsers { get; set; }
    
    /// <summary>
    /// Can ban users (admin only)
    /// </summary>
    public bool? CanBanUsers { get; set; }
    
    /// <summary>
    /// Can restrict users (mute, slow mode) (admin only)
    /// </summary>
    public bool? CanRestrictUsers { get; set; }
    
    /// <summary>
    /// Can promote members to admin (usually owner only)
    /// </summary>
    public bool? CanPromoteMembers { get; set; }
    
    // ================================================================
    // CHAT MANAGEMENT (admin only)
    // ================================================================
    
    /// <summary>
    /// Can change chat icon, name, description
    /// </summary>
    public bool? CanCustomizeGroup { get; set; }
    
    /// <summary>
    /// Can create/manage topics (for groups with topics)
    /// </summary>
    public bool? CanManageTopics { get; set; }
    
    /// <summary>
    /// Can create invite links
    /// NULL = inherit from ChatRoom.DefaultCanInviteUsers (for members)
    /// </summary>
    public bool? CanManageInviteLinks { get; set; }
    
    // ================================================================
    // CUSTOM TITLE (admin only)
    // ================================================================
    
    /// <summary>
    /// Custom rank title (e.g., "Community Manager", "Moderator", "Helper")
    /// Max 16 characters, only for admins
    /// </summary>
    public string? CustomTitle { get; set; }
    
    // ================================================================
    // NAVIGATION
    // ================================================================
    
    public ChatRoomMember Member { get; set; } = null!;
}
