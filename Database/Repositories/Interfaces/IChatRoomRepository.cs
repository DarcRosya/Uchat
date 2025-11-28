using Uchat.Database.Entities;

namespace Uchat.Database.Repositories.Interfaces;

/// <summary>
/// Интерфейс для работы с чатами и группами (ChatRoom)
/// </summary>
public interface IChatRoomRepository
{
    /// ПРИМЕЧАНИЕ: Создатель автоматически добавляется как участник с ролью Owner
    /// ВАЖНО: Устанавливает CreatedAt = DateTime.UtcNow
    Task<ChatRoom> CreateAsync(ChatRoom chatRoom);
    
    /// Добавить участника в чат
    /// ПРИМЕЧАНИЕ: Проверяет, не является ли пользователь уже участником
    /// ВАЖНО: По умолчанию роль Member, устанавливает JoinedAt
    Task AddMemberAsync(ChatRoomMember member);
    
    /// ОПТИМИЗАЦИЯ: Включает данные участников через Include
    /// ВАЖНО: Возвращает NULL если чат не найден
    Task<ChatRoom?> GetByIdAsync(int id);
    
    /// Получить все чаты пользователя
    /// ОПТИМИЗАЦИЯ: Включает данные о чате и последнем сообщении
    /// СОРТИРОВКА: По времени последнего сообщения (недавние сверху)
    Task<IEnumerable<ChatRoom>> GetUserChatRoomsAsync(int userId);
    
    /// Изменить роль участника чата
    /// ПРАВА: Обычно только владелец (Owner) может менять роли
    /// ВАЖНО: Нельзя изменить роль владельца (Owner защищен)
    Task<bool> UpdateMemberRoleAsync(int chatRoomId, int userId, ChatRoomRole role);
    
    /// Удалить участника из чата (кикнуть)
    /// ПРАВА: Админы с правом CanRemoveUsers
    /// ВАЖНО: Hard delete (полное удаление записи), можно пригласить снова
    Task<bool> RemoveMemberAsync(int chatRoomId, int userId);
}
