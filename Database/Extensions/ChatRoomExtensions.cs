
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

        // Топик - ВСЕГДА наследовать от родителя (свои настройки игнорируются)
        if (chatRoom.Type == ChatRoomType.Topic && chatRoom.ParentChatRoom != null)
        {
            return chatRoom.ParentChatRoom.GetEffectiveAllowMembersToInvite();
        }

        // Defaults по типу
        return chatRoom.DefaultCanInviteUsers ?? chatRoom.Type switch
        {
            ChatRoomType.Public => true,      // В публичных группах можно
            ChatRoomType.Private => false,    // В приватных только админы
            ChatRoomType.Channel => false,    // В каналах только админы
            _ => false
        };
    }

    /// <summary>
    /// Получить эффективное значение DefaultCanSendMessages
    /// </summary>
    public static bool GetEffectiveAllowMembersToSendMessages(this ChatRoom chatRoom)
    {
        // DirectMessage - оба всегда могут писать
        if (chatRoom.Type == ChatRoomType.DirectMessage)
            return true;

        // Топик - ВСЕГДА наследовать от родителя (свои настройки игнорируются)
        if (chatRoom.Type == ChatRoomType.Topic && chatRoom.ParentChatRoom != null)
        {
            return chatRoom.ParentChatRoom.GetEffectiveAllowMembersToSendMessages();
        }

        // Defaults по типу
        return chatRoom.DefaultCanSendMessages ?? chatRoom.Type switch
        {
            ChatRoomType.Channel => false,    // ВАЖНО! В каналах только админы
            ChatRoomType.Public => true,
            ChatRoomType.Private => true,
            _ => true
        };
    }

    /// <summary>
    /// Получить эффективное значение SlowModeSeconds
    /// </summary>
    public static int? GetEffectiveSlowModeSeconds(this ChatRoom chatRoom)
    {
        // DirectMessage - нет slow mode
        if (chatRoom.Type == ChatRoomType.DirectMessage)
            return null;

        // Топик - ВСЕГДА наследовать от родителя (свои настройки игнорируются)
        if (chatRoom.Type == ChatRoomType.Topic && chatRoom.ParentChatRoom != null)
        {
            return chatRoom.ParentChatRoom.GetEffectiveSlowModeSeconds();
        }

        return chatRoom.SlowModeSeconds;
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
    /// Это топик внутри группы?
    /// </summary>
    public static bool IsTopic(this ChatRoom chatRoom)
    {
        return chatRoom.Type == ChatRoomType.Topic && chatRoom.ParentChatRoomId.HasValue;
    }

    /// <summary>
    /// Это канал (только админы пишут)?
    /// </summary>
    public static bool IsChannel(this ChatRoom chatRoom)
    {
        return chatRoom.Type == ChatRoomType.Channel;
    }

    /// <summary>
    /// Это группа (не личный чат, не топик)?
    /// </summary>
    public static bool IsGroup(this ChatRoom chatRoom)
    {
        return chatRoom.Type is ChatRoomType.Private or ChatRoomType.Public;
    }

    /// <summary>
    /// Может ли пользователь создавать топики в этом чате?
    /// 
    /// Правила:
    /// - DirectMessage: нет
    /// - Topic: нет (топик не может содержать топики)
    /// - Group/Channel: да (если админ)
    /// </summary>
    public static bool CanHaveTopics(this ChatRoom chatRoom)
    {
        return chatRoom.Type is ChatRoomType.Private 
            or ChatRoomType.Public 
            or ChatRoomType.Channel;
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

            case ChatRoomType.Topic:
                // Topic не должен иметь описания
                if (!string.IsNullOrEmpty(chatRoom.Description))
                {
                    throw new InvalidOperationException(
                        "Topic cannot have a Description. Set it to NULL.");
                }

                // Topic не должен иметь своих настроек (наследуются от родителя)
                if (chatRoom.DefaultCanInviteUsers.HasValue ||
                    chatRoom.DefaultCanSendMessages.HasValue ||
                    chatRoom.DefaultCanSendPhotos.HasValue ||
                    chatRoom.DefaultCanSendVideos.HasValue ||
                    chatRoom.DefaultCanSendStickers.HasValue ||
                    chatRoom.DefaultCanSendMusic.HasValue ||
                    chatRoom.DefaultCanSendFiles.HasValue ||
                    chatRoom.DefaultCanPinMessages.HasValue ||
                    chatRoom.DefaultCanCustomizeGroup.HasValue ||
                    chatRoom.SlowModeSeconds.HasValue)
                {
                    throw new InvalidOperationException(
                        "Topic cannot have its own permission settings. These are inherited from ParentChatRoom.");
                }

                // Topic должен иметь родителя
                if (!chatRoom.ParentChatRoomId.HasValue)
                {
                    throw new InvalidOperationException(
                        "Topic must have ParentChatRoomId set.");
                }
                break;

            case ChatRoomType.Private:
            case ChatRoomType.Public:
            case ChatRoomType.Channel:
                // Эти типы должны НЕ иметь родителя
                if (chatRoom.ParentChatRoomId.HasValue)
                {
                    throw new InvalidOperationException(
                        $"{chatRoom.Type} cannot have ParentChatRoomId. Only Topics can have a parent.");
                }
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
    /// Получить участников топика (наследуются от родительской группы)
    /// 
    /// Использование:
    ///   var members = await context.GetTopicMembersAsync(topicId);
    /// </summary>
    public static async Task<List<User>> GetTopicMembersAsync(
        this UchatDbContext context, 
        int topicId)
    {
        var topic = await context.ChatRooms
            .Include(cr => cr.ParentChatRoom)
                .ThenInclude(p => p!.Members)
                    .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(cr => cr.Id == topicId);

        if (topic == null)
            throw new InvalidOperationException($"ChatRoom {topicId} not found");

        if (!topic.IsTopic())
            throw new InvalidOperationException($"ChatRoom {topicId} is not a topic");

        if (topic.ParentChatRoom == null)
            throw new InvalidOperationException($"Topic {topicId} has no parent group");

        return topic.ParentChatRoom.Members
            .Select(m => m.User)
            .ToList();
    }

    /// <summary>
    /// Проверить, может ли пользователь отправлять сообщения в чат
    /// 
    /// Учитывает:
    /// - Тип чата (DirectMessage, Channel, etc.)
    /// - Роль пользователя (Admin, Member, etc.)
    /// - Настройки группы (AllowMembersToSendMessages)
    /// - Наследование настроек от родителя (для топиков)
    /// 
    /// Использование:
    ///   if (!await context.CanUserSendMessageAsync(chatRoomId, userId))
    ///       throw new ForbiddenException("Cannot send messages");
    /// </summary>
    public static async Task<bool> CanUserSendMessageAsync(
        this UchatDbContext context,
        int chatRoomId,
        int userId)
    {
        var chatRoom = await context.ChatRooms
            .Include(cr => cr.ParentChatRoom)  // Для топиков
            .FirstOrDefaultAsync(cr => cr.Id == chatRoomId);

        if (chatRoom == null)
            return false;

        var member = await context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.ChatRoomId == chatRoomId && m.UserId == userId);

        if (member == null)
            return false;  // Не участник

        // Админы и владельцы всегда могут писать
        if (member.Role == ChatRoomRole.Admin || member.Role == ChatRoomRole.Owner)
            return true;

        // Проверить настройки с учётом наследования
        return chatRoom.GetEffectiveAllowMembersToSendMessages();
    }

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
                Role = ChatRoomRole.Member,
                JoinedAt = DateTime.UtcNow
            },
            new ChatRoomMember
            {
                ChatRoomId = chat.Id,
                UserId = user2Id,
                Role = ChatRoomRole.Member,
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

/*
 * ============================================================================
 * ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ
 * ============================================================================
 * 
 * 1. Проверить настройки группы:
 *    ────────────────────────────
 *    var group = await context.ChatRooms.FindAsync(5);
 *    
 *    bool canInvite = group.GetEffectiveAllowMembersToInvite();
 *    bool canWrite = group.GetEffectiveAllowMembersToSendMessages();
 *    int? slowMode = group.GetEffectiveSlowModeSeconds();
 *    
 *    if (slowMode.HasValue) {
 *        Console.WriteLine($"Slow mode: 1 message per {slowMode} seconds");
 *    }
 *    
 *    // Для топика - настройки ВСЕГДА от родителя:
 *    var topic = await context.ChatRooms
 *        .Include(cr => cr.ParentChatRoom)
 *        .FirstAsync(cr => cr.Id == topicId);
 *    
 *    bool topicCanWrite = topic.GetEffectiveAllowMembersToSendMessages();
 *    // → Вернёт ParentChatRoom.GetEffectiveAllowMembersToSendMessages()
 *    // topic.AllowMembersToSendMessages ИГНОРИРУЕТСЯ!
 * 
 * 
 * 2. Получить участников топика:
 *    ────────────────────────────
 *    var members = await context.GetTopicMembersAsync(topicId);
 *    
 *    foreach (var user in members) {
 *        Console.WriteLine($"- {user.Username}");
 *    }
 * 
 * 
 * 3. Проверить права перед отправкой сообщения:
 *    ──────────────────────────────────────────
 *    if (!await context.CanUserSendMessageAsync(chatRoomId, userId)) {
 *        return BadRequest("You cannot send messages in this chat");
 *    }
 *    
 *    // Для проверки медиа используй ChatRoomMemberPermissionsExtensions:
 *    var member = await context.ChatRoomMembers
 *        .Include(m => m.ChatRoom)
 *        .Include(m => m.Permissions)
 *        .FirstAsync(m => m.ChatRoomId == chatRoomId && m.UserId == userId);
 *    
 *    if (hasPhoto && !member.CanSendPhotos()) {
 *        return BadRequest("You cannot send photos in this chat");
 *    }
 *    
 *    // Отправить сообщение...
 * 
 * 
 * 4. Создать или получить личный чат:
 *    ─────────────────────────────────
 *    var chat = await context.GetOrCreateDirectChatAsync(currentUserId, targetUserId);
 *    
 *    // Теперь можно отправлять сообщения через MongoDB
 *    await mongoRepo.SendMessageAsync(new MongoMessage {
 *        ChatId = chat.Id,
 *        // ...
 *    });
 * 
 * 
 * 5. Обновить статистику после сообщения:
 *    ─────────────────────────────────────
 *    // В MongoMessageRepository после SendMessageAsync:
 *    await _sqliteContext.UpdateChatStatisticsAsync(chatId);
 * 
 * 
 * 6. Валидировать ChatRoom перед сохранением:
 *    ──────────────────────────────────────
 *    var topic = new ChatRoom {
 *        Type = ChatRoomType.Topic,
 *        Name = "General",
 *        ParentChatRoomId = groupId,
 *        Description = "Some description"  // ← Ошибка!
 *    };
 *    
 *    topic.ValidateForType();  // → Throws InvalidOperationException
 *    
 *    // Правильно:
 *    var topic = new ChatRoom {
 *        Type = ChatRoomType.Topic,
 *        Name = "General",
 *        ParentChatRoomId = groupId,
 *        Description = null  // ← OK
 *    };
 *    
 *    topic.ValidateForType();  // ← Успех
 *    context.ChatRooms.Add(topic);
 *    await context.SaveChangesAsync();
 * 
 * ============================================================================
 */
