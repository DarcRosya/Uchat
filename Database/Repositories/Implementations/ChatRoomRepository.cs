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

    public async Task<IEnumerable<ChatRoom>> GetUserChatRoomsAsync(int userId)
    {
        return await _context.ChatRooms
            .Include(c => c.Members)
            .Where(c => c.Members.Any(m => m.UserId == userId))
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
            .FirstOrDefaultAsync(m => m.ChatRoomId == chatRoomId && m.UserId == userId);

        if (member == null)
            return false;

        // Hard delete: удаляем участника из группы
        _context.ChatRoomMembers.Remove(member);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<ChatRoom>> GetChatRoomsByIdsAsync(IEnumerable<int> chatIds)
    {
        var ids = chatIds.Distinct().ToList();
        if (!ids.Any())
        {
            return Enumerable.Empty<ChatRoom>();
        }

        return await _context.ChatRooms
            .Include(c => c.Members)
            .Where(c => ids.Contains(c.Id))
            .ToListAsync();
    }
}
