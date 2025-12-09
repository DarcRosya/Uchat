using Microsoft.EntityFrameworkCore;
using Uchat.Database.Context;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Database.Repositories;

public class UserSecurityTokenRepository : IUserSecurityTokenRepository
{
    private readonly UchatDbContext _context;

    public UserSecurityTokenRepository(UchatDbContext context)
    {
        _context = context;
    }

    public async Task<UserSecurityToken> CreateAsync(UserSecurityToken token)
    {
        _context.Set<UserSecurityToken>().Add(token);
        await _context.SaveChangesAsync();
        return token;
    }

    public async Task<UserSecurityToken?> GetByTokenAsync(string token, TokenType type)
    {
        return await _context.Set<UserSecurityToken>()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token && t.Type == type);
    }

    public async Task<bool> MarkAsUsedAsync(int id)
    {
        var tok = await _context.Set<UserSecurityToken>().FindAsync(id);
        if (tok == null) return false;
        tok.IsUsed = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> DeleteExpiredAsync(DateTime now)
    {
        var expired = await _context.Set<UserSecurityToken>()
            .Where(t => t.ExpiresAt < now)
            .ToListAsync();

        if (!expired.Any()) return 0;

        _context.Set<UserSecurityToken>().RemoveRange(expired);
        await _context.SaveChangesAsync();
        return expired.Count;
    }
}
