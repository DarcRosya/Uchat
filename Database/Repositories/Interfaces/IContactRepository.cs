using Uchat.Database.Entities;

namespace Uchat.Database.Repositories.Interfaces;

/// <summary>
/// Interface for working with the contact list (Contact)
/// </summary>
public interface IContactRepository
{
    /// NOTE: The method checks whether the contact has already been added (duplicates).
    Task<Contact> AddContactAsync(int ownerId, int contactUserId);

    /// IMPORTANT: Returns NULL if the contact is not found.
    Task<Contact?> GetByIdAsync(int contactId);

    /// OPTIMIZATION: Includes User (ContactUser) data via Include to avoid N+1 queries
    Task<IEnumerable<Contact>> GetUserContactsAsync(int userId);
    
    /// Find a contact between two users
    /// WHY: To check if user B is in user A's contacts
    Task<Contact?> FindContactAsync(int ownerId, int contactUserId);
    
    /// Get contacts by status (Friend, RequestSent, RequestReceived, Blocked)
    Task<IEnumerable<Contact>> GetContactsByStatusAsync(int userId, ContactStatus status);
    
    /// Update contact status
    Task<bool> UpdateStatusAsync(int contactId, ContactStatus status);
    
    /// Get only selected contacts
    Task<IEnumerable<Contact>> GetFavoriteContactsAsync(int userId);
    
    /// Get blocked contacts
    Task<IEnumerable<Contact>> GetBlockedContactsAsync(int userId);
    
    /// Search for contacts by name/nickname
    /// SEARCH BY:
    /// - User's username
    /// - User's display name
    /// - Nickname (if set in the contact)
    /// - PrivateNotes (personal notes)
    Task<IEnumerable<Contact>> SearchContactsAsync(int userId, string query);

    /// Block or unblock a contact
    Task<bool> BlockContactAsync(int contactId, bool isBlocked);

    /// Add/remove from favorites
    Task<bool> SetFavoriteAsync(int contactId, bool isFavorite);
    
    /// Set the IsBlocked flag
    Task<bool> SetBlockedAsync(int contactId, bool isBlocked);

    /// Update the time of the last message
    /// WHY: Sort contacts by activity (recently communicated with - at the top)
    /// WHEN NEEDED:
    /// - Automatically when sending/receiving a message
    /// - Called from MessageRepository after saving a message
    /// - Not called directly via API (internal logic)
    Task<bool> UpdateLastMessageAsync(int contactId, DateTime? lastMessageAt);

    /// Increase the message counter with the contact
    /// 
    /// WHY: Statistics - how many messages have been sent with this contact
    /// WHEN NEEDED:
    /// - Automatically when sending/receiving a message
    /// - Called from MessageRepository
    /// - Display statistics in profile (“Total messages: 1523”)
    /// 
    /// IMPORTANT: Usually called together with UpdateLastMessageAsync
    Task<long> IncrementMessageCountAsync(int contactId, int increment = 1);
    
    /// Set nickname for contact
    Task<bool> SetNicknameAsync(int contactId, string? nickname);
    
    /// Turn notifications from a contact on/off
    Task<bool> SetNotificationsEnabledAsync(int contactId, bool enabled);
    
    /// Set SavedChatRoomId for quick access to chat
    Task<bool> SetSavedChatRoomAsync(int contactId, int? chatRoomId);
    
    /// Delete contact by ID
    Task<bool> DeleteContactAsync(int contactId);
    
    /// Delete contact from list
    Task<bool> RemoveContactAsync(int ownerId, int contactUserId);
    
    /// Check if the contact exists
    Task<bool> ExistsAsync(int ownerId, int contactUserId);
    
    /// Check if the user is blocked
    Task<bool> IsBlockedAsync(int ownerId, int contactUserId);
}
