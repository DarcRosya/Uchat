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
        // Проверим, не существует ли уже запись
        var exists = await _context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.ChatRoomId == member.ChatRoomId && m.UserId == member.UserId);

        if (exists != null)
        {
            // Если запись уже существует, обновим роль и статус мута
            exists.IsMuted = member.IsMuted;
            exists.Role = member.Role;
            exists.JoinedAt = DateTime.UtcNow;
        }
        else
        {
            member.JoinedAt = DateTime.UtcNow;
            _context.ChatRoomMembers.Add(member);
        }

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
