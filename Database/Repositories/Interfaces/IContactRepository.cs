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
    
    /// Search for contacts by name/nickname
    /// SEARCH BY:
    /// - User's username
    /// - User's display name
    /// - Nickname (if set in the contact)
    /// - PrivateNotes (personal notes)
    Task<IEnumerable<Contact>> SearchContactsAsync(int userId, string query);
    

    /// Update the time of the last message
    /// WHY: Sort contacts by activity (recently communicated with - at the top)
    /// WHEN NEEDED:
    /// - Automatically when sending/receiving a message
    /// - Called from MessageRepository after saving a message
    /// - Not called directly via API (internal logic)
    Task<bool> UpdateLastMessageAsync(int contactId, DateTime? lastMessageAt);

    /// Set SavedChatRoomId for quick access to chat
    Task<bool> SetSavedChatRoomAsync(int contactId, int? chatRoomId);
    
    /// Delete contact by ID
    Task<bool> DeleteContactAsync(int contactId);
    
    /// Delete contact from list
    Task<bool> RemoveContactAsync(int ownerId, int contactUserId);
    
    /// Check if the contact exists
    Task<bool> ExistsAsync(int ownerId, int contactUserId);
}
