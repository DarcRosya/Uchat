using Uchat.Database.Entities;

namespace Uchat.Database.Repositories.Interfaces;

/// <summary>
/// Репозиторий для работы со списком контактов (Contact)
/// </summary>
public interface IContactRepository
{
    Task<Contact> AddContactAsync(int ownerId, int contactUserId, int? groupId = null);
    Task<bool> RemoveContactAsync(int ownerId, int contactUserId);
    Task<IEnumerable<Contact>> GetUserContactsAsync(int userId, int? groupId = null);
    Task<bool> BlockContactAsync(int contactId, bool isBlocked);
    Task<bool> SetFavoriteAsync(int contactId, bool isFavorite);

    // Notes and metadata
    Task<bool> SetNotesAsync(int contactId, string? notes);
    Task<bool> MoveToGroupAsync(int contactId, int? groupId);
    Task<bool> UpdateLastMessageAsync(int contactId, DateTime? lastMessageAt);
    Task<long> IncrementMessageCountAsync(int contactId, int increment = 1);
    Task<bool> SetCustomRingtoneAsync(int contactId, string? ringtone);
    Task<bool> SetShowTypingIndicatorAsync(int contactId, bool show);

    // Contact group management
    Task<ContactGroup> CreateGroupAsync(int ownerId, string name, string? color = null);
    Task<IEnumerable<ContactGroup>> GetUserGroupsAsync(int ownerId);
    Task<bool> RenameGroupAsync(int groupId, string newName);
    Task<bool> DeleteGroupAsync(int groupId);
}
