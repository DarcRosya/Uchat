
using Microsoft.EntityFrameworkCore;
using Uchat.Database.Context;
using Uchat.Database.Entities;

namespace Uchat.Database.Extensions;

public static class ChatRoomExtensions
{
    public static bool GetEffectiveAllowMembersToInvite(this ChatRoom chatRoom)
    {
        if (chatRoom.Type == ChatRoomType.DirectMessage)
            return false;

        // Defaults по типу
        return chatRoom.Type switch
        {
            ChatRoomType.Public => true,    
            ChatRoomType.Private => false,    
            _ => false
        };
    }

    public static bool IsDirectMessage(this ChatRoom chatRoom)
    {
        return chatRoom.Type == ChatRoomType.DirectMessage;
    }

    public static bool IsGroup(this ChatRoom chatRoom)
    {
        return chatRoom.Type is ChatRoomType.Private or ChatRoomType.Public;
    }

    public static void ValidateForType(this ChatRoom chatRoom)
    {
        switch (chatRoom.Type)
        {
            case ChatRoomType.DirectMessage:
                if (!string.IsNullOrEmpty(chatRoom.Description))
                {
                    throw new InvalidOperationException(
                        "DirectMessage cannot have a Description. Set it to NULL.");
                }
                break;

            case ChatRoomType.Private:
            case ChatRoomType.Public:
                break;
        }
    }
}

public static class ChatRoomQueryExtensions
{
    public static async Task<ChatRoom> GetOrCreateDirectChatAsync(
        this UchatDbContext context,
        int user1Id,
        int user2Id)
    {
        if (user1Id == user2Id)
            throw new InvalidOperationException("Cannot create direct chat with yourself");

        // Проверить существующий чат
        var existing = await context.ChatRooms
            .Include(cr => cr.Members)
            .Where(cr => cr.Type == ChatRoomType.DirectMessage)
            .Where(cr => cr.Members.Count == 2)
            .Where(cr => cr.Members.Any(m => m.UserId == user1Id))
            .Where(cr => cr.Members.Any(m => m.UserId == user2Id))
            .FirstOrDefaultAsync();

        if (existing != null)
            return existing;

        var chat = new ChatRoom
        {
            Type = ChatRoomType.DirectMessage,
            CreatorId = user1Id,
            CreatedAt = DateTime.UtcNow,
            TotalMessagesCount = 0
        };
        context.ChatRooms.Add(chat);
        await context.SaveChangesAsync();

        context.ChatRoomMembers.AddRange(
            new ChatRoomMember
            {
                ChatRoomId = chat.Id,
                UserId = user1Id,
                JoinedAt = DateTime.UtcNow
            },
            new ChatRoomMember
            {
                ChatRoomId = chat.Id,
                UserId = user2Id,
                JoinedAt = DateTime.UtcNow
            }
        );
        await context.SaveChangesAsync();

        return chat;
    }

    public static async Task UpdateChatStatisticsAsync(
        this UchatDbContext context,
        int chatRoomId)
    {
        await context.ChatRooms
            .Where(cr => cr.Id == chatRoomId)
            .ExecuteUpdateAsync(cr => cr
                .SetProperty(x => x.TotalMessagesCount, x => x.TotalMessagesCount + 1)
                .SetProperty(x => x.LastActivityAt, DateTime.UtcNow)
            );
    }
}