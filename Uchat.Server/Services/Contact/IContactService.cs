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
    Task<ServiceResult> AcceptFriendRequestAsync(int userId, int requesterId);

    /// <summary>
    /// Отклонить заявку в друзья.
    /// Удаляет обе записи Contact.
    /// </summary>
    Task<ServiceResult> RejectFriendRequestAsync(int userId, int requesterId);

    /// <summary>
    /// Удалить из друзей.
    /// Обновляет обе записи на Status = None (остаются контакты, но не друзья).
    /// </summary>
    Task<ServiceResult> RemoveFriendAsync(int userId, int friendId);

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
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public object? Data { get; set; }

    public static ServiceResult Success(object? data = null) => new() 
    { 
        IsSuccess = true, 
        Data = data 
    };

    public static ServiceResult Failure(string error) => new() 
    { 
        IsSuccess = false, 
        ErrorMessage = error 
    };
}
