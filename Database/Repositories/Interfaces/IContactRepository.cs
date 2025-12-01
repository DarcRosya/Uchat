using Database.Entities;

namespace Database.Repositories.Interfaces;

/// <summary>
/// Интерфейс для работы со списком контактов (Contact)
/// </summary>
public interface IContactRepository
{
    /// ПРИМЕЧАНИЕ: Метод проверяет, не добавлен ли контакт уже (дубликаты)
    Task<Contact> AddContactAsync(int ownerId, int contactUserId);

    /// ВАЖНО: Возвращает NULL если контакт не найден
    Task<Contact?> GetByIdAsync(int contactId);

    /// ОПТИМИЗАЦИЯ: Включает данные User (ContactUser) через Include для избежания N+1 запросов
    Task<IEnumerable<Contact>> GetUserContactsAsync(int userId);
    
    /// Найти контакт между двумя пользователями
    /// ЗАЧЕМ: Проверить, есть ли пользователь B в контактах пользователя A
    Task<Contact?> FindContactAsync(int ownerId, int contactUserId);
    
    /// Получить только избранные контакты
    Task<IEnumerable<Contact>> GetFavoriteContactsAsync(int userId);
    
    /// Получить заблокированные контакты
    Task<IEnumerable<Contact>> GetBlockedContactsAsync(int userId);
    
    /// Поиск контактов по имени/никнейму
    /// ПОИСК ПО:
    /// - Username пользователя
    /// - DisplayName пользователя
    /// - Nickname (если установлен в контакте)
    /// - PrivateNotes (личные заметки)
    Task<IEnumerable<Contact>> SearchContactsAsync(int userId, string query);

    /// Заблокировать или разблокировать контакт
    Task<bool> BlockContactAsync(int contactId, bool isBlocked);

    /// Добавить/убрать из избранного
    Task<bool> SetFavoriteAsync(int contactId, bool isFavorite);

    /// Установить личные заметки о контакте
    Task<bool> SetNotesAsync(int contactId, string? notes);

    /// Обновить время последнего сообщения
    /// ЗАЧЕМ: Сортировка контактов по активности (с кем недавно общались - вверху)
    /// КОГДА НУЖНА:
    /// - Автоматически при отправке/получении сообщения
    /// - Вызывается из MessageRepository после сохранения сообщения
    /// - Не вызывается через API напрямую (внутренняя логика)
    Task<bool> UpdateLastMessageAsync(int contactId, DateTime? lastMessageAt);

    /// Увеличить счетчик сообщений с контактом
    /// 
    /// ЗАЧЕМ: Статистика - сколько всего сообщений отправлено с этим контактом
    /// КОГДА НУЖНА:
    /// - Автоматически при отправке/получении сообщения
    /// - Вызывается из MessageRepository
    /// - Показ статистики в профиле ("Всего сообщений: 1523")
    /// 
    /// ВАЖНО: Обычно вызывается вместе с UpdateLastMessageAsync
    Task<long> IncrementMessageCountAsync(int contactId, int increment = 1);
    
    /// Установить никнейм контакту
    Task<bool> SetNicknameAsync(int contactId, string? nickname);
    
    /// Включить/выключить уведомления от контакта
    Task<bool> SetNotificationsEnabledAsync(int contactId, bool enabled);
    
    /// Удалить контакт из списка
    Task<bool> RemoveContactAsync(int ownerId, int contactUserId);
    
    /// Проверить, существует ли контакт
    Task<bool> ExistsAsync(int ownerId, int contactUserId);
    
    /// Проверить, заблокирован ли пользователь
    Task<bool> IsBlockedAsync(int ownerId, int contactUserId);
}
