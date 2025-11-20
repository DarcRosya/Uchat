using Microsoft.EntityFrameworkCore;
using Uchat.Database.Context;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Database.Repositories;

public class UserRepository : IUserRepository
{
    private readonly UchatDbContext _context;

    public UserRepository(UchatDbContext context)
    {
        _context = context;
    }

    public async Task<User> CreateAsync(User user)
    {
        user.CreatedAt = DateTime.UtcNow;
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _context.Users.OrderBy(u => u.Username).ToListAsync();
    }

    public async Task<IEnumerable<User>> SearchByUsernameAsync(string searchTerm, int limit = 20)
    {
        var q = _context.Users
            .Where(u => EF.Functions.Like(u.Username, $"%{searchTerm}%")
                        || EF.Functions.Like(u.DisplayName, $"%{searchTerm}%"))
            .OrderBy(u => u.Username)
            .Take(limit);

        return await q.ToListAsync();
    }

    public async Task<bool> ExistsAsync(string username)
    {
        return await _context.Users.AnyAsync(u => u.Username == username);
    }

    public async Task<bool> UpdateAsync(User user)
    {
        var existing = await _context.Users.FindAsync(user.Id);
        if (existing == null)
            return false;

        _context.Entry(existing).CurrentValues.SetValues(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateStatusAsync(int userId, UserStatus status)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return false;

        user.Status = status;
        // Try to set LastSeenAt if model contains it (might be shadow property)
        try
        {
            _context.Entry(user).Property("LastSeenAt").CurrentValue = DateTime.UtcNow;
        }
        catch { }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateLastSeenAsync(int userId, DateTime lastSeen)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return false;

        try
        {
            _context.Entry(user).Property("LastSeenAt").CurrentValue = lastSeen;
        }
        catch
        {
            // If property doesn't exist, ignore
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdatePasswordAsync(int userId, string passwordHash, string salt)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return false;

        user.PasswordHash = passwordHash;
        user.Salt = salt;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return false;

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> BlockUserAsync(int userId, bool isBlocked)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return false;

        user.IsBlocked = isBlocked;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetOnlineCountAsync()
    {
        return await _context.Users.CountAsync(u => u.Status == UserStatus.Online);
    }

    public async Task<IEnumerable<User>> GetOnlineUsersAsync()
    {
        return await _context.Users
            .Where(u => u.Status == UserStatus.Online)
            .OrderBy(u => u.Username)
            .ToListAsync();
    }
}
