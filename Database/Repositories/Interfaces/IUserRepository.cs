/*
 * ============================================================================
 * REPOSITORY PATTERN (Паттерн Репозиторий)
 * ============================================================================
 * 
 * ЧТО ТАКОЕ REPOSITORY?
 * Это паттерн проектирования, который:
 * 1. Абстрагирует работу с базой данных
 * 2. Предоставляет чистый API для CRUD операций
 * 3. Отделяет бизнес-логику от логики доступа к данным
 * 
 * С Repository паттерном:
 *   await userRepository.GetByIdAsync(userId)
 * 
 * ЗАЧЕМ ЭТО НУЖНО?
 * - Код чище и понятнее
 * - Легко тестировать (можно замокать репозиторий)
 * - Если поменяешь БД (SQLite -> PostgreSQL), меняешь только репозиторий
 * - Переиспользование кода (один метод GetByIdAsync везде)
 * 
 * ============================================================================
 */

using Uchat.Database.Entities;

namespace Uchat.Database.Repositories.Interfaces;

/// <summary>
/// Интерфейс для работы с пользователями (User)
/// </summary>
public interface IUserRepository
{
    /// ПРИМЕЧАНИЕ: Проверяет уникальность username
    Task<User> CreateUserAsync(string username, string passwordHash, string? email = null);
    
    /// ВАЖНО: Возвращает NULL если пользователь не найден
    Task<User?> GetByIdAsync(int userId);
    
    /// ЗАЧЕМ: Авторизация, поиск по нику
    /// ВАЖНО: Поиск регистронезависимый
    Task<User?> GetByUsernameAsync(string username);
    
    /// ЗАЧЕМ: Восстановление пароля, проверка при регистрации
    Task<User?> GetByEmailAsync(string email);

    Task<User?> GetByUsernameOrEmailAsync(string identifier);
    
    /// Глобальный поиск пользователей
    /// ПОИСК ПО: Username, DisplayName, Bio
    Task<IEnumerable<User>> SearchUsersAsync(string query, int limit = 50);
    
    /// ЗАЧЕМ: Массовая загрузка данных (участники чата)
    Task<IEnumerable<User>> GetUsersByIdsAsync(IEnumerable<int> userIds);
    
    /// ЗАЧЕМ: Настройки профиля
    Task<bool> UpdateProfileAsync(int userId, string? displayName = null, string? bio = null, string? avatarUrl = null);
    
    /// ВАЖНО: Принимает УЖЕ хешированный новый пароль
    Task<bool> ChangePasswordAsync(int userId, string newPasswordHash);
    
    /// ПРИМЕЧАНИЕ: Проверяет уникальность email
    Task<bool> UpdateEmailAsync(int userId, string email);
    
    /// Мягкое удаление (IsDeleted = true)
    /// ЗАЧЕМ: Возможность восстановления аккаунта
    Task<bool> SoftDeleteAsync(int userId);
    
    /// Восстановить удаленного пользователя
    Task<bool> RestoreAsync(int userId);
    
    /// ЗАЧЕМ: Валидация при регистрации
    Task<bool> UsernameExistsAsync(string username);
    
    Task<bool> EmailExistsAsync(string email);
    
    /// Статистика
    Task<long> GetTotalUsersCountAsync();
}

/*
 * ============================================================================
 * ПОДСКАЗКИ ПО РАБОТЕ С REPOSITORY:
 * ============================================================================
 * 
 * 1. ПОЛУЧЕНИЕ СВЯЗАННЫХ ДАННЫХ (.Include)
 * 
 *    // Плохо - N+1 запросов (медленно!)
 *    var chatRooms = await _context.ChatRooms.ToListAsync();
 *    foreach (var room in chatRooms)
 *    {
 *        var members = room.Members; // Отдельный SQL запрос для КАЖДОГО чата!
 *    }
 *    
 *    // Хорошо - 1 запрос с JOIN
 *    var chatRooms = await _context.ChatRooms
 *        .Include(r => r.Members)
 *        .ToListAsync();
 * 
 * 
 * 2. ФИЛЬТРАЦИЯ (.Where)
 * 
 *    // Получить только активных пользователей
 *    var activeUsers = await _context.Users
 *        .Where(u => u.Status == UserStatus.Online)
 *        .ToListAsync();
 * 
 * 
 * 3. СОРТИРОВКА (.OrderBy / .OrderByDescending)
 * 
 *    // Сортировка по имени (A-Z)
 *    var users = await _context.Users
 *        .OrderBy(u => u.Username)
 *        .ToListAsync();
 *    
 *    // Сортировка по дате (новые первыми)
 *    var messages = await _context.Messages
 *        .OrderByDescending(m => m.SentAt)
 *        .ToListAsync();
 * 
 * 
 * 4. ПАГИНАЦИЯ (.Skip / .Take)
 * 
 *    // Страница 3, по 20 записей на странице
 *    int page = 3;
 *    int pageSize = 20;
 *    
 *    var users = await _context.Users
 *        .OrderBy(u => u.Username)
 *        .Skip((page - 1) * pageSize)  // Пропустить первые 40
 *        .Take(pageSize)                // Взять 20
 *        .ToListAsync();
 * 
 * ============================================================================
 */
