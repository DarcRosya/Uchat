using Microsoft.EntityFrameworkCore;
using Uchat.Database.Context;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Database.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly UchatDbContext _context;

    public RefreshTokenRepository(UchatDbContext context)
    {
        _context = context;
    }
    public async Task<RefreshToken> CreateAsync(RefreshToken token)
    {
        if (token == null)
            throw new ArgumentNullException(nameof(token));

        if(string.IsNullOrEmpty(token.TokenHash))
            throw new ArgumentException("TokenHash is required", nameof(token));

        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync();
        return token;
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            return null;

        return await _context.RefreshTokens
            .Include(t => t.User)
            .Where(t =>
                t.TokenHash == tokenHash &&
                !t.IsRevoked &&
                t.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> RevokeTokenAsync(string tokenHash)
    {
        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (token == null)
            return false;

        token.IsRevoked = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> RevokeAllUserTokensAsync(int userId)
    {
        var tokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
        }

        await _context.SaveChangesAsync();  
        return tokens.Count;
    }

    public async Task<int> CleanupExpiredTokensAsync()
    {
        var now = DateTime.UtcNow;

        var expiredTokens = await _context.RefreshTokens
            .Where(t => t.IsRevoked || t.ExpiresAt < now)
            .ToListAsync();

        _context.RefreshTokens.RemoveRange(expiredTokens);
        await _context.SaveChangesAsync();

        return expiredTokens.Count;
    }
}