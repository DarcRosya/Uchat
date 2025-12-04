using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
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

    // CREATE
    public async Task<User> CreateUserAsync(string username, string passwordHash, string? email = null)
    {
        // Проверка уникальности
        if (await UsernameExistsAsync(username))
            throw new InvalidOperationException($"Username '{username}' already exists");
        
        if (email != null && await EmailExistsAsync(email))
            throw new InvalidOperationException($"Email '{email}' already exists");

        var user = new User
        {
            Username = username,
            PasswordHash = passwordHash,
            DisplayName = username,
            Email = email ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    // READ
    public async Task<User?> GetByIdAsync(int userId)
    {
        return await _context.Users.FindAsync(userId);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<User?> GetByUsernameOrEmailAsync(string identifier)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Username == identifier || u.Email.ToLower() == identifier.ToLower());
    }

    public async Task<IEnumerable<User>> SearchUsersAsync(string query, int limit = 50)
    {
        var searchTerm = query.ToLower();
        
        return await _context.Users
            .Where(u => 
                u.Username.ToLower().Contains(searchTerm) ||
                u.DisplayName.ToLower().Contains(searchTerm))
            .OrderBy(u => u.Username)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IEnumerable<User>> GetUsersByIdsAsync(IEnumerable<int> userIds)
    {
        return await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync();
    }

    // UPDATE
    public async Task<bool> UpdateProfileAsync(int userId, string? displayName = null, string? bio = null, string? avatarUrl = null)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return false;

        if (displayName != null)
            user.DisplayName = displayName;
        
        if (avatarUrl != null)
            user.AvatarUrl = avatarUrl;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangePasswordAsync(int userId, string newPasswordHash)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return false;

        user.PasswordHash = newPasswordHash;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateEmailAsync(int userId, string email)
    {
        // Проверка уникальности
        var existingUser = await GetByEmailAsync(email);
        if (existingUser != null && existingUser.Id != userId)
            throw new InvalidOperationException($"Email '{email}' already in use");

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return false;

        user.Email = email;
        await _context.SaveChangesAsync();
        return true;
    }

    // DELETE
    public async Task<bool> SoftDeleteAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return false;

        user.IsDeleted = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RestoreAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return false;

        user.IsDeleted = false;
        await _context.SaveChangesAsync();
        return true;
    }

    // UTILITY
    public async Task<bool> UsernameExistsAsync(string username)
    {
        return await _context.Users
            .AnyAsync(u => u.Username.ToLower() == username.ToLower());
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Users
            .AnyAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<long> GetTotalUsersCountAsync()
    {
        return await _context.Users.LongCountAsync();
    }

    // Вспомогательные методы
    private string GenerateSalt()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }
}

