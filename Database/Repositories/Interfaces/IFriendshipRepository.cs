using Uchat.Database.Entities;

namespace Uchat.Database.Repositories.Interfaces;

/// <summary>
/// Интерфейс для работы с дружбой и заявками в друзья (Friendship)
/// </summary>
public interface IFriendshipRepository
{
    /// ПРИМЕЧАНИЕ: Проверяет, не отправлена ли заявка уже
    /// ВАЖНО: Создает запись со статусом Pending
    Task<Friendship> CreateRequestAsync(int senderId, int receiverId);
    
    /// ВАЖНО: Возвращает NULL если заявка не найдена
    Task<Friendship?> GetByIdAsync(int id);
    
    /// ОПТИМИЗАЦИЯ: Включает данные пользователей через Include
    Task<IEnumerable<Friendship>> GetUserFriendshipsAsync(int userId);
    
    /// Получить входящие заявки в друзья (статус Pending)
    Task<IEnumerable<Friendship>> GetPendingReceivedAsync(int userId);
    
    /// Принять заявку в друзья
    /// ВАЖНО: Меняет статус на Accepted, устанавливает AcceptedAt
    Task<bool> AcceptRequestAsync(int friendshipId, int acceptedById);
    
    /// Отклонить заявку в друзья
    /// ВАЖНО: Меняет статус на Rejected
    Task<bool> RejectRequestAsync(int friendshipId, int rejectedById);
    
    /// Удалить дружбу / отменить заявку
    Task<bool> DeleteAsync(int friendshipId);
    
    /// Проверить, существует ли заявка между пользователями
    /// ОПТИМИЗАЦИЯ: Использует AnyAsync (быстрее чем получение объекта)
    Task<bool> ExistsRequestAsync(int senderId, int receiverId);
}
