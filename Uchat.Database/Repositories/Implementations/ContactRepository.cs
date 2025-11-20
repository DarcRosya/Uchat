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
            throw new InvalidOperationException("Нельзя добавить себя в контакты");

        var exists = await _context.Contacts
            .FirstOrDefaultAsync(c => c.OwnerId == ownerId && c.ContactUserId == contactUserId);

        if (exists != null)
            return exists;

        var contact = new Contact
        {
            OwnerId = ownerId,
            ContactUserId = contactUserId,
            AddedAt = DateTime.UtcNow,
            IsBlocked = false,
            IsFavorite = false
        };

        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync();
        return contact;
    }

    public async Task<Contact> AddContactAsync(int ownerId, int contactUserId, int? groupId = null)
    {
        if (ownerId == contactUserId)
            throw new InvalidOperationException("Нельзя добавить себя в контакты");

        var exists = await _context.Contacts
            .FirstOrDefaultAsync(c => c.OwnerId == ownerId && c.ContactUserId == contactUserId);

        if (exists != null)
            return exists;

        var contact = new Contact
        {
            OwnerId = ownerId,
            ContactUserId = contactUserId,
            AddedAt = DateTime.UtcNow,
            IsBlocked = false,
            IsFavorite = false,
            GroupId = groupId
        };

        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync();
        return contact;
    }

    public async Task<bool> RemoveContactAsync(int ownerId, int contactUserId)
    {
        var contact = await _context.Contacts
            .FirstOrDefaultAsync(c => c.OwnerId == ownerId && c.ContactUserId == contactUserId);

        if (contact == null)
            return false;

        _context.Contacts.Remove(contact);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<Contact>> GetUserContactsAsync(int userId)
    {
        return await _context.Contacts
            .Include(c => c.ContactUser)
            .Where(c => c.OwnerId == userId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Contact>> GetUserContactsAsync(int userId, int? groupId = null)
    {
        var q = _context.Contacts
            .Include(c => c.ContactUser)
            .Where(c => c.OwnerId == userId);

        if (groupId.HasValue)
            q = q.Where(c => c.GroupId == groupId.Value);

        return await q.ToListAsync();
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

    public async Task<bool> SetFavoriteAsync(int contactId, bool isFavorite)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
        if (contact == null)
            return false;
        contact.IsFavorite = isFavorite;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetNotesAsync(int contactId, string? notes)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
        if (contact == null)
            return false;
        contact.Notes = notes;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MoveToGroupAsync(int contactId, int? groupId)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
        if (contact == null)
            return false;
        contact.GroupId = groupId;
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

    public async Task<bool> SetCustomRingtoneAsync(int contactId, string? ringtone)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
        if (contact == null)
            return false;
        contact.CustomRingtone = ringtone;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetShowTypingIndicatorAsync(int contactId, bool show)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
        if (contact == null)
            return false;
        contact.ShowTypingIndicator = show;
        await _context.SaveChangesAsync();
        return true;
    }

    // ContactGroup management
    public async Task<ContactGroup> CreateGroupAsync(int ownerId, string name, string? color = null)
    {
        var exists = await _context.ContactGroups.FirstOrDefaultAsync(g => g.OwnerId == ownerId && g.Name == name);
        if (exists != null)
            return exists;

        var group = new ContactGroup { OwnerId = ownerId, Name = name, Color = color };
        _context.ContactGroups.Add(group);
        await _context.SaveChangesAsync();
        return group;
    }

    public async Task<IEnumerable<ContactGroup>> GetUserGroupsAsync(int ownerId)
    {
        return await _context.ContactGroups.Where(g => g.OwnerId == ownerId).ToListAsync();
    }

    public async Task<bool> RenameGroupAsync(int groupId, string newName)
    {
        var group = await _context.ContactGroups.FirstOrDefaultAsync(g => g.Id == groupId);
        if (group == null)
            return false;
        group.Name = newName;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteGroupAsync(int groupId)
    {
        var group = await _context.ContactGroups.FirstOrDefaultAsync(g => g.Id == groupId);
        if (group == null)
            return false;

        // Before deleting, set GroupId = null for contacts in this group
        var contacts = await _context.Contacts.Where(c => c.GroupId == groupId).ToListAsync();
        foreach (var c in contacts)
            c.GroupId = null;

        _context.ContactGroups.Remove(group);
        await _context.SaveChangesAsync();
        return true;
    }
}
