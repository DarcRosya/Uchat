using Microsoft.EntityFrameworkCore;
using Database.Context;
using Database.Entities;
using Database.Repositories.Interfaces;

namespace Database.Repositories;

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

    public async Task<IEnumerable<ChatRoom>> GetUserChatRoomsAsync(int userId)
    {
        return await _context.ChatRoomMembers
            .Include(m => m.ChatRoom)
            .Where(m => m.UserId == userId)
            .Select(m => m.ChatRoom)
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

    public async Task<bool> UpdateMemberRoleAsync(int chatRoomId, int userId, ChatRoomRole role)
    {
        var member = await _context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.ChatRoomId == chatRoomId && m.UserId == userId);

        if (member == null)
            return false;

        member.Role = role;
        await _context.SaveChangesAsync();
        return true;
    }
}
