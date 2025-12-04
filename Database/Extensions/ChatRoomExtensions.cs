
using Microsoft.EntityFrameworkCore;
using Uchat.Database.Context;
using Uchat.Database.Entities;

namespace Uchat.Database.Extensions;

/// <summary>
/// Extension методы для ChatRoom
/// Помогают работать с разными типами чатов единообразно
/// </summary>
public static class ChatRoomExtensions
{
    // ========================================================================
    // ПОЛУЧЕНИЕ ЭФФЕКТИВНЫХ НАСТРОЕК (с учётом наследования от родителя)
    // ========================================================================

    /// <summary>
    /// Получить эффективное значение DefaultCanInviteUsers
    /// 
    /// Для топиков проверяет ParentChatRoom, если собственное значение NULL
    /// </summary>
    public static bool GetEffectiveAllowMembersToInvite(this ChatRoom chatRoom)
    {
        // DirectMessage - нельзя приглашать (всегда 2 участника)
        if (chatRoom.Type == ChatRoomType.DirectMessage)
            return false;

        // Defaults по типу
        return chatRoom.Type switch
        {
            ChatRoomType.Public => true,      // В публичных группах можно
            ChatRoomType.Private => false,    // В приватных только админы
            _ => false
        };
    }

    // ========================================================================
    // ВАЛИДАЦИЯ И ПРОВЕРКИ
    // ========================================================================

    /// <summary>
    /// Это личный чат (1-on-1)?
    /// </summary>
    public static bool IsDirectMessage(this ChatRoom chatRoom)
    {
        return chatRoom.Type == ChatRoomType.DirectMessage;
    }

    /// <summary>
    /// Это группа (не личный чат, не топик)?
    /// </summary>
    public static bool IsGroup(this ChatRoom chatRoom)
    {
        return chatRoom.Type is ChatRoomType.Private or ChatRoomType.Public;
    }

    /// <summary>
    /// Валидировать поля ChatRoom для конкретного типа
    /// 
    /// Проверяет, что поля соответствуют правилам типа:
    /// - DirectMessage: Description = NULL
    /// - Topic: Description = NULL, AllowMembers* = NULL
    /// </summary>
    public static void ValidateForType(this ChatRoom chatRoom)
    {
        switch (chatRoom.Type)
        {
            case ChatRoomType.DirectMessage:
                // DirectMessage не должен иметь описания
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

/// <summary>
/// Query extensions для работы с ChatRoom через DbContext
/// </summary>
public static class ChatRoomQueryExtensions
{
    /// <summary>
    /// Получить существующий DirectMessage между двумя пользователями
    /// или создать новый
    /// 
    /// Использование:
    ///   var chat = await context.GetOrCreateDirectChatAsync(user1Id, user2Id);
    /// </summary>
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

        // Создать новый
        var chat = new ChatRoom
        {
            Type = ChatRoomType.DirectMessage,
            CreatorId = user1Id,
            CreatedAt = DateTime.UtcNow,
            TotalMessagesCount = 0
        };
        context.ChatRooms.Add(chat);
        await context.SaveChangesAsync();

        // Добавить участников
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

    /// <summary>
    /// Обновить статистику чата при новом сообщении
    /// 
    /// Использование (после отправки сообщения в MongoDB):
    ///   await mongoRepo.SendMessageAsync(...);
    ///   await context.UpdateChatStatisticsAsync(chatRoomId);
    /// </summary>
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