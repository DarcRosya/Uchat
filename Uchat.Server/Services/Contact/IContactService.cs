using Uchat.Database.Entities;

namespace Uchat.Server.Services.Contact;

/// <summary>
/// Бизнес-логика для работы с контактами и заявками в друзья.
/// Заменяет FriendshipService - теперь все через таблицу Contact.
/// </summary>
public interface IContactService
{
    /// <summary>
    /// Отправить заявку в друзья.
    /// Создает две записи Contact (двустороннюю связь):
    /// - User1 → User2 (Status = RequestSent)
    /// - User2 → User1 (Status = RequestReceived)
    /// </summary>
    Task<ServiceResult> SendFriendRequestAsync(int senderId, int receiverId);

    /// <summary>
    /// Принять заявку в друзья.
    /// Обновляет обе записи на Status = Friend.
    /// </summary>
    Task<ServiceResult<int>> AcceptFriendRequestAsync(int userId, int requesterId);

    /// <summary>
    /// Отклонить заявку в друзья.
    /// Удаляет обе записи Contact.
    /// </summary>
    Task<ServiceResult<int>> RejectFriendRequestAsync(int currentUserId, int contactId);

    /// <summary>
    /// Удалить из друзей.
    /// Обновляет обе записи на Status = None (остаются контакты, но не друзья).
    /// </summary>
    Task<ServiceResult> RemoveFriendAsync(int userId, int friendId);

    Task<ServiceResult> UpdateContactChatIdAsync(int userId1, int userId2, int chatRoomId);

    /// <summary>
    /// Заблокировать пользователя.
    /// Обновляет запись на Status = Blocked + IsBlocked = true.
    /// </summary>
    Task<ServiceResult> BlockUserAsync(int userId, int blockedUserId);

    /// <summary>
    /// Разблокировать пользователя.
    /// Возвращает Status = None, IsBlocked = false.
    /// </summary>
    Task<ServiceResult> UnblockUserAsync(int userId, int blockedUserId);

    /// <summary>
    /// Получить список друзей (Status = Friend).
    /// </summary>
    Task<IEnumerable<Database.Entities.Contact>> GetFriendsAsync(int userId);

    /// <summary>
    /// Получить входящие заявки в друзья (Status = RequestReceived).
    /// </summary>
    Task<IEnumerable<Database.Entities.Contact>> GetIncomingRequestsAsync(int userId);

    /// <summary>
    /// Получить исходящие заявки (Status = RequestSent).
    /// </summary>
    Task<IEnumerable<Database.Entities.Contact>> GetOutgoingRequestsAsync(int userId);

    /// <summary>
    /// Получить заблокированных пользователей (Status = Blocked).
    /// </summary>
    Task<IEnumerable<Database.Entities.Contact>> GetBlockedUsersAsync(int userId);

    /// <summary>
    /// Получить контакт по ID.
    /// </summary>
    Task<ServiceResult<Database.Entities.Contact>> GetContactByIdAsync(int contactId);

    /// <summary>
    /// Получить все контакты пользователя (друзья).
    /// </summary>
    Task<ServiceResult<IEnumerable<Database.Entities.Contact>>> GetContactsAsync(int userId);

    /// <summary>
    /// Получить входящие запросы в друзья.
    /// </summary>
    Task<ServiceResult<IEnumerable<Database.Entities.Contact>>> GetPendingRequestsAsync(int userId);

    /// <summary>
    /// Добавить в избранное.
    /// </summary>
    Task<ServiceResult> SetFavoriteAsync(int userId, int contactUserId, bool isFavorite);

    /// <summary>
    /// Установить никнейм для контакта.
    /// </summary>
    Task<ServiceResult> SetNicknameAsync(int userId, int contactUserId, string? nickname);
}

/// <summary>
/// Результат операции сервиса (успех/ошибка).
/// </summary>
public class ServiceResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int Data { get; internal set; }

    public static ServiceResult SuccessResult() => new() { Success = true };
    public static ServiceResult Failure(string error) => new() { Success = false, Message = error };
}

public class ServiceResult<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    public static ServiceResult<T> SuccessResult(T? data = default) => new() { Success = true, Data = data };
    public static ServiceResult<T> Failure(string error) => new() { Success = false, Message = error };
}
