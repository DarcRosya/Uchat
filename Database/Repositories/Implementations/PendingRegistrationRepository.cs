using Microsoft.EntityFrameworkCore;
using Uchat.Database.Context;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Database.Repositories;

public class PendingRegistrationRepository : IPendingRegistrationRepository
{
    private readonly UchatDbContext _context;

    public PendingRegistrationRepository(UchatDbContext context)
    {
        _context = context;
    }

    public async Task<PendingRegistration> CreateOrUpdateAsync(PendingRegistration pending)
    {
        var existing = await _context.Set<PendingRegistration>()
            .FirstOrDefaultAsync(p => p.Email == pending.Email);

        if (existing != null)
        {
            // Обновляем существующую
            existing.Code = pending.Code;
            existing.CodeExpiresAt = pending.CodeExpiresAt;
            existing.PasswordHash = pending.PasswordHash; 
            existing.Username = pending.Username;
            existing.CreatedAt = DateTime.UtcNow; 
            
            _context.Set<PendingRegistration>().Update(existing);
        }
        else
        {
            await _context.Set<PendingRegistration>().AddAsync(pending);
        }

        await _context.SaveChangesAsync();
        return existing ?? pending;
    }

    public async Task<PendingRegistration?> GetByEmailAsync(string email)
    {
        return await _context.Set<PendingRegistration>()
            .FirstOrDefaultAsync(p => p.Email.ToLower() == email.ToLower());
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        return await _context.Set<PendingRegistration>()
            .AnyAsync(p => p.Username.ToLower() == username.ToLower());
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Set<PendingRegistration>()
            .AnyAsync(p => p.Email.ToLower() == email.ToLower());
    }

    public async Task<int> DeleteExpiredAsync()
    {
        var now = DateTime.UtcNow;
        
        return await _context.Set<PendingRegistration>()
            .Where(p => p.CodeExpiresAt.HasValue && p.CodeExpiresAt < now)
            .ExecuteDeleteAsync();
    }

    public async Task<bool> DeleteAsync(string email)
    {
        // Тоже используем ExecuteDelete для эффективности
        var rowsDeleted = await _context.Set<PendingRegistration>()
            .Where(p => p.Email.ToLower() == email.ToLower())
            .ExecuteDeleteAsync();
            
        return rowsDeleted > 0;
    }
}