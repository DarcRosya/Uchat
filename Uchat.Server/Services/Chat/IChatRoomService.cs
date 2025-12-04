using System.Collections.Generic;
using System.Threading.Tasks;
using Uchat.Database.Entities;

namespace Uchat.Server.Services.Chat;

/// <summary>
/// Service contract для операций с чатами и проверки прав доступа.
/// </summary>
public interface IChatRoomService
{
    /// <summary>
    /// Получить список всех чатов пользователя
    /// </summary>
    Task<List<ChatRoom>> GetUserChatsAsync(int userId);

    /// <summary>
    /// Получить детали чата с проверкой прав доступа
    /// </summary>
    Task<ChatResult> GetChatDetailsAsync(int chatId, int userId);

    /// <summary>
    /// Создать новый чат
    /// </summary>
    Task<ChatResult> CreateChatAsync(int creatorId, string name, ChatRoomType type, string? description, IEnumerable<int>? initialMemberIds);

    /// <summary>
    /// Добавить участника в чат
    /// </summary>
    Task<ChatResult> AddMemberAsync(int chatId, int actorUserId, int memberUserId);

    /// <summary>
    /// Удалить участника из чата
    /// </summary>
    Task<ChatResult> RemoveMemberAsync(int chatId, int actorUserId, int memberUserId);

    Task<ChatResult> IsUserInChatAsync(int userId, int chatId);
}
