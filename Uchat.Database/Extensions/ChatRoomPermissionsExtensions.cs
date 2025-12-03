using Uchat.Database.Entities;

namespace Uchat.Database.Extensions;

/// <summary>
/// Extension methods for three-level permission system:
/// 1. GLOBAL (ChatRoom defaults) - for all members
/// 2. ADMIN (ChatRoomMemberPermissions) - admin rights
/// 3. EXCEPTIONS (ChatRoomMemberPermissions) - individual overrides
/// </summary>
public static class ChatRoomMemberPermissionsExtensions
{
    /// <summary>
    /// Get default admin permissions (all rights enabled)
    /// </summary>
    public static ChatRoomMemberPermissions GetDefaultAdminPermissions(this ChatRoomMember member)
    {
        return new ChatRoomMemberPermissions
        {
            ChatRoomMemberId = member.Id,
            
            // Messages - admins can send everything
            CanSendMessages = true,
            CanSendPhotos = true,
            CanSendVideos = true,
            CanSendStickers = true,
            CanSendMusic = true,
            CanSendFiles = true,
            CanDeleteMessages = true,
            CanPinMessages = true,
            
            // Members - full management except promotion
            CanInviteUsers = true,
            CanRemoveUsers = true,
            CanBanUsers = true,
            CanRestrictUsers = true,
            CanPromoteMembers = false, // Only owner by default
            
            // Chat - basic management
            CanCustomizeGroup = true,
            CanManageTopics = true,
            CanManageInviteLinks = true,
            
            CustomTitle = null
        };
    }

    /// <summary>
    /// Full owner permissions (everything enabled)
    /// </summary>
    public static ChatRoomMemberPermissions GetOwnerPermissions(this ChatRoomMember member)
    {
        return new ChatRoomMemberPermissions
        {
            ChatRoomMemberId = member.Id,
            
            // Everything enabled
            CanSendMessages = true,
            CanSendPhotos = true,
            CanSendVideos = true,
            CanSendStickers = true,
            CanSendMusic = true,
            CanSendFiles = true,
            CanDeleteMessages = true,
            CanPinMessages = true,
            CanInviteUsers = true,
            CanRemoveUsers = true,
            CanBanUsers = true,
            CanRestrictUsers = true,
            CanPromoteMembers = true,
            CanCustomizeGroup = true,
            CanManageTopics = true,
            CanManageInviteLinks = true,
            
            CustomTitle = "Owner"
        };
    }
    
    // ========================================================================
    // THREE-LEVEL PERMISSION RESOLUTION
    // ========================================================================
    
    /// <summary>
    /// Resolve effective permission using three-level system:
    /// 1. Check if admin/owner (bypass most restrictions)
    /// 2. Check individual permission (exception or admin right)
    /// 3. Fall back to ChatRoom default
    /// 4. Fall back to hardcoded default
    /// </summary>
    public static bool CanSendMessages(this ChatRoomMember member)
    {
        var chatRoom = member.ChatRoom;
        
        // Level 1: Owner/Admin bypass (unless explicitly restricted)
        if (member.Role == ChatRoomRole.Owner)
            return member.Permissions?.CanSendMessages ?? true;
        
        if (member.Role == ChatRoomRole.Admin)
            return member.Permissions?.CanSendMessages ?? true;
        
        // Level 2: Individual exception
        if (member.Permissions?.CanSendMessages != null)
            return member.Permissions.CanSendMessages.Value;
        
        // Level 3: Global ChatRoom setting
        if (chatRoom.DefaultCanSendMessages != null)
            return chatRoom.DefaultCanSendMessages.Value;
        
        // Level 4: Type default
        return chatRoom.Type switch
        {
            ChatRoomType.Channel => false,
            _ => true
        };
    }
    
    // ========================================================================
    // MEDIA PERMISSIONS (5 types)
    // ========================================================================
    
    public static bool CanSendPhotos(this ChatRoomMember member)
    {
        var chatRoom = member.ChatRoom;
        
        if (member.Role >= ChatRoomRole.Admin)
            return member.Permissions?.CanSendPhotos ?? true;
        
        if (member.Permissions?.CanSendPhotos != null)
            return member.Permissions.CanSendPhotos.Value;
        
        if (chatRoom.DefaultCanSendPhotos != null)
            return chatRoom.DefaultCanSendPhotos.Value;
        
        return true;
    }
    
    public static bool CanSendVideos(this ChatRoomMember member)
    {
        var chatRoom = member.ChatRoom;
        
        if (member.Role >= ChatRoomRole.Admin)
            return member.Permissions?.CanSendVideos ?? true;
        
        if (member.Permissions?.CanSendVideos != null)
            return member.Permissions.CanSendVideos.Value;
        
        if (chatRoom.DefaultCanSendVideos != null)
            return chatRoom.DefaultCanSendVideos.Value;
        
        return true;
    }
    
    public static bool CanSendStickers(this ChatRoomMember member)
    {
        var chatRoom = member.ChatRoom;
        
        if (member.Role >= ChatRoomRole.Admin)
            return member.Permissions?.CanSendStickers ?? true;
        
        if (member.Permissions?.CanSendStickers != null)
            return member.Permissions.CanSendStickers.Value;
        
        if (chatRoom.DefaultCanSendStickers != null)
            return chatRoom.DefaultCanSendStickers.Value;
        
        return true;
    }
    
    public static bool CanSendMusic(this ChatRoomMember member)
    {
        var chatRoom = member.ChatRoom;
        
        if (member.Role >= ChatRoomRole.Admin)
            return member.Permissions?.CanSendMusic ?? true;
        
        if (member.Permissions?.CanSendMusic != null)
            return member.Permissions.CanSendMusic.Value;
        
        if (chatRoom.DefaultCanSendMusic != null)
            return chatRoom.DefaultCanSendMusic.Value;
        
        return true;
    }
    
    public static bool CanSendFiles(this ChatRoomMember member)
    {
        var chatRoom = member.ChatRoom;
        
        if (member.Role >= ChatRoomRole.Admin)
            return member.Permissions?.CanSendFiles ?? true;
        
        if (member.Permissions?.CanSendFiles != null)
            return member.Permissions.CanSendFiles.Value;
        
        if (chatRoom.DefaultCanSendFiles != null)
            return chatRoom.DefaultCanSendFiles.Value;
        
        return true;
    }
    
    public static bool CanInviteUsers(this ChatRoomMember member)
    {
        var chatRoom = member.ChatRoom;
        
        // Admins/Owner can always invite
        if (member.Role >= ChatRoomRole.Admin)
            return member.Permissions?.CanInviteUsers ?? true;
        
        // Individual exception
        if (member.Permissions?.CanInviteUsers != null)
            return member.Permissions.CanInviteUsers.Value;
        
        // Global default
        if (chatRoom.DefaultCanInviteUsers != null)
            return chatRoom.DefaultCanInviteUsers.Value;
        
        // Type default
        return chatRoom.Type switch
        {
            ChatRoomType.Public => true,
            ChatRoomType.Private => false,
            ChatRoomType.Channel => false,
            _ => false
        };
    }
    
    public static bool CanPinMessages(this ChatRoomMember member)
    {
        var chatRoom = member.ChatRoom;
        
        // Admin/Owner check personal permissions first
        if (member.Role >= ChatRoomRole.Admin)
            return member.Permissions?.CanPinMessages ?? true;
        
        // Individual exception
        if (member.Permissions?.CanPinMessages != null)
            return member.Permissions.CanPinMessages.Value;
        
        // Global default
        if (chatRoom.DefaultCanPinMessages != null)
            return chatRoom.DefaultCanPinMessages.Value;
        
        return false; // Default: members can't pin
    }
    
    public static bool CanCustomizeGroup(this ChatRoomMember member)
    {
        var chatRoom = member.ChatRoom;
        
        // Admin/Owner check personal permissions first
        if (member.Role >= ChatRoomRole.Admin)
            return member.Permissions?.CanCustomizeGroup ?? true;
        
        // Individual exception
        if (member.Permissions?.CanCustomizeGroup != null)
            return member.Permissions.CanCustomizeGroup.Value;
        
        // Global default
        if (chatRoom.DefaultCanCustomizeGroup != null)
            return chatRoom.DefaultCanCustomizeGroup.Value;
        
        return false; // Default: only admins can customize
    }
    
    // ========================================================================
    // ADMIN-ONLY PERMISSIONS
    // ========================================================================
    
    public static bool CanDeleteMessages(this ChatRoomMember member)
    {
        if (member.Role == ChatRoomRole.Owner)
            return true;
        
        if (member.Role == ChatRoomRole.Admin)
            return member.Permissions?.CanDeleteMessages ?? true;
        
        return false;
    }
    
    public static bool CanBanUsers(this ChatRoomMember member)
    {
        if (member.Role == ChatRoomRole.Owner)
            return true;
        
        if (member.Role == ChatRoomRole.Admin)
            return member.Permissions?.CanBanUsers ?? false;
        
        return false;
    }
    
    public static bool CanPromoteMembers(this ChatRoomMember member)
    {
        if (member.Role == ChatRoomRole.Owner)
            return true;
        
        if (member.Role == ChatRoomRole.Admin)
            return member.Permissions?.CanPromoteMembers ?? false;
        
        return false;
    }
}
