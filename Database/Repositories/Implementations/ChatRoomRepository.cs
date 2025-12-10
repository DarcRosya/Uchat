using System.Linq;
using Microsoft.EntityFrameworkCore;
using Uchat.Database.Context;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Database.Repositories;

public class ChatRoomRepository : IChatRoomRepository
{
    private readonly UchatDbContext _context;

    public ChatRoomRepository(UchatDbContext context)
    {
        _context = context;
    }

    public async Task<ChatRoom> CreateAsync(ChatRoom chatRoom)
    {
        chatRoom.CreatedAt = DateTime.UtcNow;
        _context.ChatRooms.Add(chatRoom);
        await _context.SaveChangesAsync();
        return chatRoom;
    }

    public async Task<ChatRoom?> GetByIdAsync(int id)
    {
        return await _context.ChatRooms
            .Include(cr => cr.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(cr => cr.Id == id);
    }
    
    public async Task<ChatRoom?> GetByNameAsync(string name)
    {
        return await _context.ChatRooms
            .Include(cr => cr.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(cr => cr.Name == name);
    }

    public async Task<List<ChatRoomMember>> GetUserChatMembershipsAsync(int userId)
    {
        return await _context.ChatRoomMembers
            .Include(m => m.ChatRoom)
                .ThenInclude(cr => cr.Members) 
            .Where(m => m.UserId == userId && !m.IsDeleted && !m.IsPending)
            .OrderByDescending(m => m.ChatRoom.LastActivityAt ?? m.ChatRoom.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<ChatRoomMember>> GetPendingMembershipsAsync(int userId)
    {
        return await _context.ChatRoomMembers
            .Include(m => m.ChatRoom)   
            .Include(m => m.InvitedBy)  
            .Where(m => m.UserId == userId && m.IsPending && !m.IsDeleted)
            .ToListAsync();
    }

    public async Task AddMemberAsync(ChatRoomMember member)
    {
        if (member.JoinedAt == default)
        {
            member.JoinedAt = DateTime.UtcNow;
        }

        _context.ChatRoomMembers.Add(member);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ChatRoom chatRoom)
    {
        _context.ChatRooms.Update(chatRoom);
        
        await _context.SaveChangesAsync();
    }

    public async Task<ChatRoomMember?> GetMemberAsync(int chatRoomId, int userId)
    {
        return await _context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.ChatRoomId == chatRoomId && m.UserId == userId);
    }

    public async Task UpdateMemberAsync(ChatRoomMember member)
    {
        _context.ChatRoomMembers.Update(member);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveMemberEntityAsync(ChatRoomMember member)
    {
        _context.ChatRoomMembers.Remove(member);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> RemoveMemberAsync(int chatRoomId, int userId)
    {
        var member = await _context.ChatRoomMembers
            .FirstOrDefaultAsync(crm => crm.ChatRoomId == chatRoomId && crm.UserId == userId);

        if (member == null)
            return false;

        member.ClearedHistoryAt = DateTime.UtcNow;
        member.IsDeleted = true;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<ChatRoomMember>> GetMembersForUserByChatIdsAsync(int userId, IEnumerable<int> chatIds)
    {
        var ids = chatIds.Distinct().ToList();
        
        if (!ids.Any())
        {
            return new List<ChatRoomMember>();
        }

        return await _context.ChatRoomMembers
            .Include(m => m.ChatRoom)           
                .ThenInclude(cr => cr.Members)  
            .Where(m => m.UserId == userId     
                        && ids.Contains(m.ChatRoomId)   
                        && !m.IsDeleted
                        && !m.IsPending)       
            .ToListAsync();
    }
}
