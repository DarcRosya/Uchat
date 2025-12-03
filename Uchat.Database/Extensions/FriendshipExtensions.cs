using Microsoft.EntityFrameworkCore;
using Uchat.Database.Context;
using Uchat.Database.Entities;

namespace Uchat.Database.Extensions;

/// <summary>
/// Extension methods for working with friendships and getting friends list
/// </summary>
public static class FriendshipExtensions
{
    /// <summary>
    /// Get all accepted friends for a user
    /// Uses optimized indexes: IX_Friendships_Sender_Status and IX_Friendships_Receiver_Status
    /// </summary>
    public static async Task<List<User>> GetFriendsAsync(
        this UchatDbContext context, 
        int userId)
    {
        // Get friendships where user is sender OR receiver AND status is Accepted
        var friendships = await context.Friendships
            .Include(f => f.Sender)
            .Include(f => f.Receiver)
            .Where(f => f.Status == FriendshipStatus.Accepted)
            .Where(f => f.SenderId == userId || f.ReceiverId == userId)
            .ToListAsync();
        
        // Extract the "other" user from each friendship
        var friends = friendships
            .Select(f => f.SenderId == userId ? f.Receiver : f.Sender)
            .ToList();
        
        return friends;
    }
    
    /// <summary>
    /// Get pending incoming friend requests (received by user)
    /// Uses index: IX_Friendships_Receiver_Status
    /// </summary>
    public static async Task<List<Friendship>> GetPendingRequestsAsync(
        this UchatDbContext context, 
        int userId)
    {
        return await context.Friendships
            .Include(f => f.Sender)
            .Where(f => f.ReceiverId == userId && f.Status == FriendshipStatus.Pending)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
    }
    
    /// <summary>
    /// Get pending outgoing friend requests (sent by user)
    /// Uses index: IX_Friendships_Sender_Status
    /// </summary>
    public static async Task<List<Friendship>> GetSentRequestsAsync(
        this UchatDbContext context, 
        int userId)
    {
        return await context.Friendships
            .Include(f => f.Receiver)
            .Where(f => f.SenderId == userId && f.Status == FriendshipStatus.Pending)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
    }
    
    /// <summary>
    /// Check if two users are friends
    /// Uses index: IX_Friendships_Sender_Receiver
    /// </summary>
    public static async Task<bool> AreFriendsAsync(
        this UchatDbContext context, 
        int userId1, 
        int userId2)
    {
        return await context.Friendships
            .AnyAsync(f => 
                f.Status == FriendshipStatus.Accepted &&
                ((f.SenderId == userId1 && f.ReceiverId == userId2) ||
                 (f.SenderId == userId2 && f.ReceiverId == userId1)));
    }
    
    /// <summary>
    /// Get friendship between two users (if exists)
    /// </summary>
    public static async Task<Friendship?> GetFriendshipAsync(
        this UchatDbContext context, 
        int userId1, 
        int userId2)
    {
        return await context.Friendships
            .FirstOrDefaultAsync(f => 
                (f.SenderId == userId1 && f.ReceiverId == userId2) ||
                (f.SenderId == userId2 && f.ReceiverId == userId1));
    }
}
