/*
 * ============================================================================
 * ENTITY MODEL: USER (Пользователь)
 * ============================================================================
 * 
 * ЧТО ЭТО?
 * Это модель данных для пользователя в базе данных.
 * В FastAPI это был бы класс с SQLAlchemy Base, например:
 * 
 *   class User(Base):
 *       __tablename__ = "users"
 *       id = Column(Integer, primary_key=True)
 *       username = Column(String, unique=True)
 * 
 * В C# с Entity Framework Core мы делаем то же самое, но по-другому:
 * - Свойства (properties) = колонки в таблице
 * - Navigation properties = связи с другими таблицами (ForeignKey)
 * 
 * ============================================================================
 * ЗАЧЕМ НУЖНЫ НАВИГАЦИОННЫЕ СВОЙСТВА?
 * ============================================================================
 * 
 * В FastAPI/SQLAlchemy ты использовал relationship():
 *   messages = relationship("Message", back_populates="sender")
 * 
 * В C# это делается через ICollection<T>:
 *   public ICollection<Message> SentMessages { get; set; }
 * 
 * Это позволяет делать:
 *   var user = await _context.Users.Include(u => u.SentMessages).FirstAsync();
 *   // Теперь user.SentMessages содержит все сообщения пользователя!
 * 
 * ============================================================================
 */

namespace Uchat.Database.Entities;

/// <summary>
/// Модель пользователя в системе чата
/// Представляет таблицу Users в базе данных SQLite
/// </summary>
public class User
{
    // ========================================================================
    // ОСНОВНЫЕ ПОЛЯ (Primary Key и основная информация)
    // ========================================================================
    
    /// <summary>
    /// Уникальный идентификатор пользователя (Primary Key)
    /// В SQLite это будет INTEGER PRIMARY KEY AUTOINCREMENT
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Уникальное имя пользователя (логин)
    /// Используется для входа в систему
    /// В БД: VARCHAR(50), UNIQUE, NOT NULL
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Хеш пароля (НЕ ХРАНИМ ПАРОЛЬ В ОТКРЫТОМ ВИДЕ!)
    /// Обычно используем SHA256, bcrypt или Argon2
    /// В БД: VARCHAR(256), NOT NULL
    /// 
    /// Пример создания хеша:
    ///   using System.Security.Cryptography;
    ///   var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password + salt));
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Соль для хеширования пароля (случайная строка)
    /// Делает хеш уникальным даже для одинаковых паролей
    /// В БД: VARCHAR(128), NOT NULL
    /// 
    /// Пример генерации соли:
    ///   var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    /// </summary>
    public string Salt { get; set; } = string.Empty;
    
    // ========================================================================
    // ДОПОЛНИТЕЛЬНАЯ ИНФОРМАЦИЯ О ПОЛЬЗОВАТЕЛЕ
    // ========================================================================
    
    /// <summary>
    /// Email пользователя (обязательное поле, индексируется)
    /// В БД: VARCHAR(255), NOT NULL, INDEX
    /// </summary>
    public string Email { get; set; } = string.Empty;
        /// <summary>
        /// Биография пользователя (максимум 190 символов)
        /// В БД: VARCHAR(190), NULL
        /// </summary>
        public string? Bio { get; set; }

        /// <summary>
        /// Номер телефона пользователя (максимум 20 символов, nullable, индексируется)
        /// В БД: VARCHAR(20), NULL, INDEX
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Дата рождения пользователя
        /// В БД: DATE, NULL
        /// </summary>
        public DateTime? BirthDate { get; set; }

        /// <summary>
        /// Статус пользователя (Offline, Online, Away, DoNotDisturb)
        /// В БД: INTEGER (enum), NOT NULL, DEFAULT Offline
        /// </summary>
        public UserStatus Status { get; set; } = UserStatus.Offline;

        /// <summary>
        /// Настройки языка пользователя (ISO code, например "en", "uk", "en-US")
        /// Хранится как отдельный класс
        /// </summary>
        public UserLanguage Language { get; set; } = new UserLanguage { Code = "en" };
    
    /// <summary>
    /// Отображаемое имя пользователя (то, что видят другие)
    /// Может отличаться от Username
    /// В БД: VARCHAR(100), NOT NULL
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// URL аватара пользователя (путь к картинке)
    /// Может быть локальный путь или URL из интернета
    /// В БД: VARCHAR(500), NULL
    /// </summary>
    public string? AvatarUrl { get; set; }
    
    // ========================================================================
    // ВРЕМЕННЫЕ МЕТКИ (Timestamps)
    // ========================================================================
    
    /// <summary>
    /// Дата и время создания аккаунта
    /// В БД: DATETIME, DEFAULT CURRENT_TIMESTAMP
    /// 
    /// Сохраняем в UTC чтобы избежать проблем с часовыми поясами:
    ///   CreatedAt = DateTime.UtcNow;
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    // ========================================================================
    // СТАТУСЫ И ФЛАГИ
    // ========================================================================
    
    /// <summary>
    /// Заблокирован ли пользователь (бан)
    /// В БД: BOOLEAN (в SQLite это 0 или 1), NOT NULL, DEFAULT 0
    /// </summary>
    public bool IsBlocked { get; set; }
    
    // ========================================================================
    // НАВИГАЦИОННЫЕ СВОЙСТВА (Relationships / Foreign Keys)
    // ========================================================================
    // Это НЕ хранится в таблице Users! Это виртуальные связи с другими таблицами.
    // Entity Framework автоматически загружает эти данные когда нужно.
    // ========================================================================
    
    /// <summary>
    /// Все групповые чаты, в которых участвует пользователь
    /// 
    /// Связь: User (Many) -> ChatRoomMember (Many) -> ChatRoom (Many)
    /// Это Many-to-Many связь через промежуточную таблицу ChatRoomMembers
    /// 
    /// Как использовать:
    ///   var user = await context.Users
    ///       .Include(u => u.ChatRoomMemberships)
    ///           .ThenInclude(m => m.ChatRoom)  // Загружаем сами чаты
    ///       .FirstAsync(u => u.Id == 1);
    ///   
    ///   foreach (var membership in user.ChatRoomMemberships) {
    ///       Console.WriteLine($"В чате: {membership.ChatRoom.Name}");
    ///       Console.WriteLine($"Роль: {membership.Role}");
    ///   }
    /// </summary>
    public ICollection<ChatRoomMember> ChatRoomMemberships { get; set; } = new List<ChatRoomMember>();
    
    /// <summary>
    /// Список контактов (друзей) этого пользователя
    /// 
    /// Связь: User (1) -> Contact (Many)
    /// Foreign Key в таблице Contacts: OwnerId -> Users.Id
    /// </summary>
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
}

/// <summary>
/// Перечисление статусов пользователя
/// </summary>
public enum UserStatus
{
    Offline = 0,
    Online = 1,
    Away = 2,
    DoNotDisturb = 3
}

/// <summary>
/// Класс для хранения языка пользователя
/// </summary>
public class UserLanguage
{
    /// <summary>
    /// Код языка (например, "en", "uk", "en-US")
    /// </summary>
    public string Code { get; set; } = "en";
}
