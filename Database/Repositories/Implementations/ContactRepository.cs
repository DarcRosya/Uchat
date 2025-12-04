using Microsoft.EntityFrameworkCore;
using Uchat.Database.Context;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Database.Repositories;

public class ContactRepository : IContactRepository
{
    private readonly UchatDbContext _context;

    public ContactRepository(UchatDbContext context)
    {
        _context = context;
    }

    public async Task<Contact> AddContactAsync(int ownerId, int contactUserId)
    {
        if (ownerId == contactUserId)
            throw new InvalidOperationException("You cannot add yourself to contacts.");

        var exists = await _context.Contacts
            .FirstOrDefaultAsync(c => c.OwnerId == ownerId && c.ContactUserId == contactUserId);

        if (exists != null)
            return exists;

        var contact = new Contact
        {
            OwnerId = ownerId,
            ContactUserId = contactUserId,
            Status = ContactStatus.None,
            AddedAt = DateTime.UtcNow,
            IsBlocked = false,
            IsFavorite = false
        };

        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync();
        return contact;
    }

    public async Task<Contact?> GetByIdAsync(int contactId)
    {
        return await _context.Contacts
            .Include(c => c.ContactUser)
            .Include(c => c.SavedChatRoom)
            .FirstOrDefaultAsync(c => c.Id == contactId);
    }

    public async Task<Contact?> FindContactAsync(int ownerId, int contactUserId)
    {
        return await _context.Contacts
            .Include(c => c.ContactUser)
            .Include(c => c.SavedChatRoom)
            .FirstOrDefaultAsync(c => 
                c.OwnerId == ownerId && 
                c.ContactUserId == contactUserId
            );
    }

    public async Task<IEnumerable<Contact>> GetUserContactsAsync(int userId)
    {
        return await _context.Contacts
            .Include(c => c.ContactUser)
            .Include(c => c.SavedChatRoom)
            .Where(c => c.OwnerId == userId)
            .OrderByDescending(c => c.IsFavorite)
            .ThenByDescending(c => c.LastMessageAt)
            .ThenBy(c => c.ContactUser.Username)  
            .ToListAsync();
    }

    public async Task<IEnumerable<Contact>> GetContactsByStatusAsync(int userId, ContactStatus status)
    {
        return await _context.Contacts
            .Include(c => c.ContactUser)
            .Include(c => c.SavedChatRoom)
            .Where(c => c.OwnerId == userId && c.Status == status)
            .OrderBy(c => c.ContactUser.Username)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Contact>> GetFavoriteContactsAsync(int userId)
    {
        return await _context.Contacts
            .Include(c => c.ContactUser)
            .Include(c => c.SavedChatRoom)
            .Where(c => c.IsFavorite && c.OwnerId == userId)
            .OrderBy(c => c.ContactUser.Username)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Contact>> GetBlockedContactsAsync(int userId)
    {
        return await _context.Contacts
            .Include(c => c.ContactUser)
            .Include(c => c.SavedChatRoom)
            .Where(c => c.IsBlocked && c.OwnerId == userId)
            .OrderBy(c => c.ContactUser.Username)
            .ToListAsync();
    }

    public async Task<IEnumerable<Contact>> SearchContactsAsync(int userId, string query)
    {
        var searchTerm = query.ToLower();

        return await _context.Contacts
            .Include(c => c.ContactUser)
            .Where(c => 
                c.OwnerId == userId &&
                (
                    c.ContactUser.Username.ToLower().Contains(searchTerm) ||
                    (c.ContactUser.DisplayName != null && 
                     c.ContactUser.DisplayName.ToLower().Contains(searchTerm)) ||
                    (c.Nickname != null && 
                     c.Nickname.ToLower().Contains(searchTerm))
                )
            )
            .OrderBy(c => c.ContactUser.Username)
            .ToListAsync();
    }

    // ========================================================================
    // UPDATE
    // ========================================================================
    
    public async Task<bool> UpdateStatusAsync(int contactId, ContactStatus status)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
        if (contact == null)
            return false;

        contact.Status = status;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetFavoriteAsync(int contactId, bool isFavorite)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
        if (contact == null)
            return false;

        contact.IsFavorite = isFavorite;
        await _context.SaveChangesAsync();
        return true;
    }
    
    public async Task<bool> SetBlockedAsync(int contactId, bool isBlocked)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
        if (contact == null)
            return false;

        contact.IsBlocked = isBlocked;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetNicknameAsync(int contactId, string? nickname)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
        if (contact == null) 
            return false;

        contact.Nickname = nickname;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetNotificationsEnabledAsync(int contactId, bool enabled)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
        if (contact == null) 
            return false;
        
        contact.NotificationsEnabled = enabled;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateLastMessageAsync(int contactId, DateTime? lastMessageAt)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
        if (contact == null)
            return false;

        contact.LastMessageAt = lastMessageAt;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<long> IncrementMessageCountAsync(int contactId, int increment = 1)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
        if (contact == null) 
            return 0;

        contact.MessageCount += increment;
        await _context.SaveChangesAsync();
        return contact.MessageCount;
    }

    public async Task<bool> SetSavedChatRoomAsync(int contactId, int? chatRoomId)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
        if (contact == null)
            return false;

        contact.SavedChatRoomId = chatRoomId;
        await _context.SaveChangesAsync();
        return true;
    }

    // ========================================================================
    // DELETE
    // ========================================================================
    
    public async Task<bool> DeleteContactAsync(int contactId)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
        if (contact == null)
            return false;

        _context.Contacts.Remove(contact);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveContactAsync(int ownerId, int contactUserId)
    {
        var contact = await _context.Contacts
            .FirstOrDefaultAsync(c => 
                c.OwnerId == ownerId && 
                c.ContactUserId == contactUserId
            );

        if (contact == null)
            return false;

        _context.Contacts.Remove(contact);
        await _context.SaveChangesAsync();
        return true;
    }

    // ========================================================================
    // UTILITY
    // ========================================================================
    
    public async Task<bool> ExistsAsync(int ownerId, int contactUserId)
    {
        return await _context.Contacts
            .AnyAsync(c => 
                c.OwnerId == ownerId && 
                c.ContactUserId == contactUserId
            );
    }
    
    public async Task<bool> IsBlockedAsync(int ownerId, int contactUserId)
    {
        var contact = await _context.Contacts
            .FirstOrDefaultAsync(c => 
                c.OwnerId == ownerId && 
                c.ContactUserId == contactUserId
            );
        
        return contact?.IsBlocked ?? false;
    }
    
    public async Task<bool> BlockContactAsync(int contactId, bool isBlocked)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
        if (contact == null)
            return false;

        contact.IsBlocked = isBlocked;
        await _context.SaveChangesAsync();
        return true;
    }
    
}
