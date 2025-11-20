using Microsoft.EntityFrameworkCore;
using Uchat.Database.Context;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Database.Repositories;

public class FriendshipRepository : IFriendshipRepository
{
    private readonly UchatDbContext _context;

    public FriendshipRepository(UchatDbContext context)
    {
        _context = context;
    }

    public async Task<Friendship> CreateRequestAsync(int senderId, int receiverId)
    {
        if (senderId == receiverId)
            throw new InvalidOperationException("Нельзя отправлять запрос самому себе");

        var exists = await _context.Friendships
            .FirstOrDefaultAsync(f => f.SenderId == senderId && f.ReceiverId == receiverId);

        if (exists != null)
            return exists;

        var friendship = new Friendship
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Friendships.Add(friendship);
        await _context.SaveChangesAsync();
        return friendship;
    }

    public async Task<Friendship?> GetByIdAsync(int id)
    {
        return await _context.Friendships.FindAsync(id);
    }

    public async Task<IEnumerable<Friendship>> GetUserFriendshipsAsync(int userId)
    {
        return await _context.Friendships
            .Where(f => (f.SenderId == userId || f.ReceiverId == userId) && f.Status == FriendshipStatus.Accepted)
            .ToListAsync();
    }

    public async Task<IEnumerable<Friendship>> GetPendingReceivedAsync(int userId)
    {
        return await _context.Friendships
            .Where(f => f.ReceiverId == userId && f.Status == FriendshipStatus.Pending)
            .ToListAsync();
    }

    public async Task<bool> AcceptRequestAsync(int friendshipId, int acceptedById)
    {
        var f = await _context.Friendships.FirstOrDefaultAsync(x => x.Id == friendshipId);
        if (f == null)
            return false;

        if (f.ReceiverId != acceptedById)
            return false; // only receiver can accept

        f.Status = FriendshipStatus.Accepted;
        f.AcceptedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectRequestAsync(int friendshipId, int rejectedById)
    {
        var f = await _context.Friendships.FirstOrDefaultAsync(x => x.Id == friendshipId);
        if (f == null)
            return false;

        if (f.ReceiverId != rejectedById)
            return false; // only receiver can reject

        f.Status = FriendshipStatus.Rejected;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int friendshipId)
    {
        var f = await _context.Friendships.FirstOrDefaultAsync(x => x.Id == friendshipId);
        if (f == null)
            return false;
        _context.Friendships.Remove(f);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsRequestAsync(int senderId, int receiverId)
    {
        return await _context.Friendships.AnyAsync(f => f.SenderId == senderId && f.ReceiverId == receiverId);
    }
}
