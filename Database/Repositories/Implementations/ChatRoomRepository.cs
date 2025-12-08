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
            .Where(m => m.UserId == userId && !m.IsDeleted) 
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
}
