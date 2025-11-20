/*
 * ============================================================================
 * DATABASE CONTEXT (Контекст базы данных)
 * ============================================================================
 * 
 * ЭТО САМЫЙ ВАЖНЫЙ ФАЙЛ ДЛЯ РАБОТЫ С БАЗОЙ ДАННЫХ!
 * 
 * ЧТО ТАКОЕ DbContext?
 * В FastAPI/SQLAlchemy это примерно эквивалент:
 *   - declarative_base() + engine + sessionmaker вместе
 * 
 * DbContext это:
 * 1. Точка входа в базу данных
 * 2. Хранилище всех таблиц (DbSet<T>)
 * 3. Место для настройки связей между таблицами
 * 4. Менеджер транзакций
 * 5. Change tracker (отслеживает изменения в объектах)
 * 
 * ============================================================================
 * КАК ЭТО РАБОТАЕТ?
 * ============================================================================
 * 
 * 1. Создаешь экземпляр DbContext
 * 2. Работаешь с DbSet<T> (это как таблицы)
 * 3. Вызываешь SaveChangesAsync() чтобы сохранить в БД
 * 
 * Пример:
 *   var context = new UchatDbContext(options);
 *   
 *   var user = new User { Username = "john" };
 *   context.Users.Add(user);  // Добавили в память
 *   await context.SaveChangesAsync();  // Сохранили в БД (INSERT)
 *   
 *   user.DisplayName = "John Doe";  // Изменили
 *   await context.SaveChangesAsync();  // EF автоматически создаст UPDATE!
 * 
 * ============================================================================
 */

using Microsoft.EntityFrameworkCore;
using Uchat.Database.Entities;

namespace Uchat.Database.Context;

/// <summary>
/// Контекст базы данных для приложения Uchat
/// 
/// Этот класс:
/// - Определяет все таблицы через DbSet
/// - Настраивает связи между таблицами (OnModelCreating)
/// - Управляет подключением к SQLite
/// </summary>
public class UchatDbContext : DbContext
{
    // ========================================================================
    // DBSETS (Таблицы в базе данных)
    // ========================================================================
    // DbSet<T> - это коллекция всех записей типа T в БД
    // Каждый DbSet представляет одну таблицу
    // ========================================================================
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<ChatRoom> ChatRooms { get; set; } = null!;
    public DbSet<ChatRoomMember> ChatRoomMembers { get; set; } = null!;
    public DbSet<ChatRoomMemberPermissions> ChatRoomMemberPermissions { get; set; } = null!;
    public DbSet<Contact> Contacts { get; set; } = null!;
    public DbSet<Friendship> Friendships { get; set; } = null!;

    // ========================================================================
    // CONSTRUCTOR (Конструктор)
    // ========================================================================
    
    /// <summary>
    /// Конструктор DbContext
    /// 
    /// DbContextOptions содержит настройки подключения к БД:
    /// - Тип БД (SQLite, PostgreSQL, MySQL...)
    /// - Connection string (путь к файлу БД)
    /// - Logging
    /// - и другое
    /// 
    /// Эти options передаются через Dependency Injection в реальном приложении:
    /// 
    ///   services.AddDbContext&lt;UchatDbContext&gt;(options =>
    ///       options.UseSqlite("Data Source=uchat.db"));
    /// </summary>
    public UchatDbContext(DbContextOptions<UchatDbContext> options) : base(options)
    {
        // base(options) передает настройки в родительский класс DbContext
    }

    // ========================================================================
    // MODEL CONFIGURATION (Конфигурация моделей)
    // ========================================================================
    // OnModelCreating вызывается ОДИН РАЗ при создании контекста
    // Здесь настраиваются:
    // - Primary Keys
    // - Foreign Keys
    // - Indexes (для ускорения запросов)
    // - Unique constraints
    // - Default values
    // - Column types
    // - Relationships (связи между таблицами)
    // ========================================================================

    /// <summary>
    /// Настройка моделей базы данных (маппинг)
    /// 
    /// В FastAPI/SQLAlchemy это делается прямо в моделях через Column(), relationship()
    /// В EF Core есть два подхода:
    /// 1. Data Annotations (атрибуты на свойствах) - проще
    /// 2. Fluent API (здесь в OnModelCreating) - мощнее и гибче
    /// 
    /// Мы используем Fluent API!
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ====================================================================
        // КОНФИГУРАЦИЯ ТАБЛИЦЫ USERS
        // ====================================================================
        
        modelBuilder.Entity<User>(entity =>
        {
            // Имя таблицы в БД (по умолчанию было бы "User")
            entity.ToTable("Users");

            // entity.HasKey(u => u.Id) не обязательно - EF автоматически определяет свойство Id как PK
            entity.HasKey(u => u.Id);

            // ----------------------------------------------------------------
            // INDEXES (индексы для быстрого поиска)
            // ----------------------------------------------------------------

            entity.HasIndex(u => u.Username)
                .IsUnique() 
                .HasDatabaseName("IX_Users_Username");
            
            // UNIQUE INDEX на Email (для входа и уникальности)
            entity.HasIndex(u => u.Email)
                .IsUnique() 
                .HasDatabaseName("IX_Users_Email");

            // ----------------------------------------------------------------
            // COLUMN CONSTRAINTS (ограничения на колонки)
            // ----------------------------------------------------------------
            
            entity.Property(u => u.Username)
                .IsRequired()  // NOT NULL
                .HasMaxLength(50);  // VARCHAR(50)

            entity.Property(u => u.Bio)
                .HasMaxLength(190);

            entity.Property(u => u.DateOfBirth);

            entity.Property(u => u.PasswordHash)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(u => u.Salt)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(u => u.DisplayName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(u => u.PhoneNumber)
                .HasMaxLength(20); 

            entity.Property(u => u.AvatarUrl)
                .HasMaxLength(500);

            // DEFAULT VALUE для CreatedAt
            // PostgreSQL: CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
            // SQLite: CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            entity.Property(u => u.CreatedAt)
                .HasDefaultValueSql("NOW()");  // PostgreSQL синтаксис (SQLite тоже поддерживает)

            entity.Property(u => u.LanguageCode)
                .IsRequired()
                .HasMaxLength(5)  // "en", "uk", "en-US"
                .HasDefaultValue("en");

            // ----------------------------------------------------------------
            // RELATIONSHIPS (связи с другими таблицами)
            // ----------------------------------------------------------------
            
            // ПРИМЕЧАНИЕ: Сообщения (Messages) хранятся в MongoDB!
            // SQLite используется только для:
            // - Users (пользователи)
            // - Contacts (контакты)
            // - Friendships (запросы в друзья)
            // - ChatRooms (метаданные чатов)
            // - ChatRoomMembers (участники чатов)

            // User -> ChatRoomMemberships (One-to-Many)
            entity.HasMany(u => u.ChatRoomMemberships)
                .WithOne(crm => crm.User)
                .HasForeignKey(crm => crm.UserId)
                .OnDelete(DeleteBehavior.Cascade);  // Удалить user → удалить его членство в группах

            // User -> Contacts (One-to-Many)
            entity.HasMany(u => u.Contacts)
                .WithOne(c => c.Owner)
                .HasForeignKey(c => c.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);  // Удалить user → удалить его контакты
        });

        // ====================================================================
        // КОНФИГУРАЦИЯ ТАБЛИЦЫ CHATROOMS
        // ====================================================================
        
        modelBuilder.Entity<ChatRoom>(entity =>
        {
            entity.ToTable("ChatRooms");
            entity.HasKey(cr => cr.Id);

            // ----------------------------------------------------------------
            // INDEXES
            // ----------------------------------------------------------------
            
            // INDEX на Type (для фильтрации по типу чата)
            entity.HasIndex(cr => cr.Type)
                .HasDatabaseName("IX_ChatRooms_Type");
            
            // INDEX на CreatorId (для получения чатов пользователя)
            entity.HasIndex(cr => cr.CreatorId)
                .HasDatabaseName("IX_ChatRooms_CreatorId");
            
            // INDEX на ParentChatRoomId (для получения топиков группы)
            entity.HasIndex(cr => cr.ParentChatRoomId)
                .HasDatabaseName("IX_ChatRooms_ParentChatRoomId");
            
            // INDEX на LastActivityAt (для сортировки по активности)
            entity.HasIndex(cr => cr.LastActivityAt)
                .HasDatabaseName("IX_ChatRooms_LastActivityAt");

            // ----------------------------------------------------------------
            // COLUMNS
            // ----------------------------------------------------------------

            entity.Property(cr => cr.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(cr => cr.Description)
                .HasMaxLength(300);

            entity.Property(cr => cr.IconUrl)
                .HasMaxLength(500);

            entity.Property(cr => cr.CreatedAt)
                .HasDefaultValueSql("NOW()");

            // ----------------------------------------------------------------
            // RELATIONSHIPS
            // ----------------------------------------------------------------

            // ChatRoom -> Creator (Many-to-One)
            entity.HasOne(cr => cr.Creator)
                .WithMany()  // У User нет обратной навигации к созданным группам
                .HasForeignKey(cr => cr.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);  // Нельзя удалить создателя

            // ChatRoom -> Members (One-to-Many)
            entity.HasMany(cr => cr.Members)
                .WithOne(crm => crm.ChatRoom)
                .HasForeignKey(crm => crm.ChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);  // Удалить группу → удалить всех участников

            // ChatRoom -> ParentChatRoom (self-reference для топиков)
            entity.HasOne(cr => cr.ParentChatRoom)
                .WithMany(cr => cr.Topics)
                .HasForeignKey(cr => cr.ParentChatRoomId)
                .OnDelete(DeleteBehavior.Cascade); // Удалить группу → удалить топики
        });

        // ====================================================================
        // КОНФИГУРАЦИЯ ТАБЛИЦЫ CHATROOMMEMBERS
        // ====================================================================
        
        modelBuilder.Entity<ChatRoomMember>(entity =>
        {
            entity.ToTable("ChatRoomMembers");
            entity.HasKey(crm => crm.Id);

            // ----------------------------------------------------------------
            // ОЧЕНЬ ВАЖНО! UNIQUE CONSTRAINT
            // ----------------------------------------------------------------
            // Пользователь НЕ МОЖЕТ быть дважды в одной группе!
            // ----------------------------------------------------------------
            
            entity.HasIndex(crm => new { crm.ChatRoomId, crm.UserId })
                .IsUnique()  // UNIQUE (ChatRoomId, UserId)
                .HasDatabaseName("IX_ChatRoomMembers_ChatRoom_User");

            entity.HasIndex(crm => crm.UserId)
                .HasDatabaseName("IX_ChatRoomMembers_UserId");

            entity.Property(crm => crm.JoinedAt)
                .HasDefaultValueSql("NOW()");

            // ChatRoomMember -> InvitedBy (кто пригласил)
            entity.HasOne(crm => crm.InvitedBy)
                .WithMany()  // У User нет списка "кого я пригласил"
                .HasForeignKey(crm => crm.InvitedById)
                .OnDelete(DeleteBehavior.SetNull);  // Удалить пригласившего → InvitedById = NULL
            
            // ChatRoomMember -> Permissions (1-to-1, optional)
            entity.HasOne(crm => crm.Permissions)
                .WithOne(p => p.Member)
                .HasForeignKey<ChatRoomMemberPermissions>(p => p.ChatRoomMemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ====================================================================
        // КОНФИГУРАЦИЯ ТАБЛИЦЫ CONTACTS
        // ====================================================================
        
        modelBuilder.Entity<Contact>(entity =>
        {
            entity.ToTable("Contacts");
            entity.HasKey(c => c.Id);

            // ----------------------------------------------------------------
            // UNIQUE CONSTRAINT
            // ----------------------------------------------------------------
            // Нельзя добавить один контакт дважды!
            // ----------------------------------------------------------------
            
            entity.HasIndex(c => new { c.OwnerId, c.ContactUserId })
                .IsUnique()
                .HasDatabaseName("IX_Contacts_Owner_Contact");

            entity.HasIndex(c => c.ContactUserId)
                .HasDatabaseName("IX_Contacts_ContactUserId");

            entity.Property(c => c.Nickname)
                .HasMaxLength(100);
            
            entity.Property(c => c.PrivateNotes)
                .HasMaxLength(500);

            entity.Property(c => c.AddedAt)
                .HasDefaultValueSql("NOW()");
            
            entity.Property(c => c.NotificationsEnabled)
                .HasDefaultValue(true);

            entity.Property(c => c.IsFavorite)
                .HasDefaultValue(false);

            // Contact -> ContactUser
            entity.HasOne(c => c.ContactUser)
                .WithMany()  // У User нет списка "кто меня добавил в контакты"
                .HasForeignKey(c => c.ContactUserId)
                .OnDelete(DeleteBehavior.Cascade);  // Удалить user → удалить контакты с ним
        });
        // ====================================================================
        // КОНФИГУРАЦИЯ ТАБЛИЦЫ CHATROOMMEMBERPERMISSIONS
        // ====================================================================
        
        modelBuilder.Entity<ChatRoomMemberPermissions>(entity =>
        {
            entity.ToTable("ChatRoomMemberPermissions");
            entity.HasKey(p => p.Id);
            
            // Index for finding member's permissions
            entity.HasIndex(p => p.ChatRoomMemberId)
                .IsUnique()
                .HasDatabaseName("IX_ChatRoomMemberPermissions_MemberId");
            
            entity.Property(p => p.CustomTitle)
                .HasMaxLength(16);
        });
        
        // ====================================================================
        // КОНФИГУРАЦИЯ ТАБЛИЦЫ FRIENDSHIPS
        // ====================================================================
        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.ToTable("Friendships");
            entity.HasKey(f => f.Id);

            // ----------------------------------------------------------------
            // UNIQUE CONSTRAINT
            // ----------------------------------------------------------------
            // Один пользователь не может отправить запрос другому дважды!
            // ----------------------------------------------------------------

            entity.HasIndex(f => new { f.SenderId, f.ReceiverId })
                .IsUnique()
                .HasDatabaseName("IX_Friendships_Sender_Receiver");
            
            // INDEX для получения входящих запросов
            entity.HasIndex(f => f.ReceiverId)
                .HasDatabaseName("IX_Friendships_ReceiverId");

            // INDEX для фильтрации по статусу
            entity.HasIndex(f => f.Status)
                .HasDatabaseName("IX_Friendships_Status");
            
            // COMPOSITE INDEX для получения списка друзей
            // Query: WHERE (SenderId = X OR ReceiverId = X) AND Status = Accepted
            entity.HasIndex(f => new { f.SenderId, f.Status })
                .HasDatabaseName("IX_Friendships_Sender_Status");
            
            entity.HasIndex(f => new { f.ReceiverId, f.Status })
                .HasDatabaseName("IX_Friendships_Receiver_Status");

            // ----------------------------------------------------------------
            // COLUMNS
            // ----------------------------------------------------------------

            entity.Property(f => f.Status)
                .HasDefaultValue(FriendshipStatus.Pending);

            entity.Property(f => f.CreatedAt)
                .HasDefaultValueSql("NOW()");

            // ----------------------------------------------------------------
            // RELATIONSHIPS (Связи)
            // ----------------------------------------------------------------
            
            // Friendship -> Sender (кто отправил запрос)
            // Обратная навигация: User.SentFriendshipRequests
            entity.HasOne(f => f.Sender)
                .WithMany() 
                .HasForeignKey(f => f.SenderId)
                .OnDelete(DeleteBehavior.Cascade);  // Удалить user → удалить его запросы

            // Friendship -> Receiver (кто получил запрос)
            // Обратная навигация: User.ReceivedFriendshipRequests
            entity.HasOne(f => f.Receiver)
                .WithMany(u => u.ReceivedFriendshipRequests)
                .HasForeignKey(f => f.ReceiverId)
                .OnDelete(DeleteBehavior.Cascade);  // Удалить user → удалить запросы к нему
        });
    }
}

/*
 * ============================================================================
 * КАК ИСПОЛЬЗОВАТЬ ЭТОТ DBCONTEXT?
 * ============================================================================
 * 
 * 1. В Program.cs сервера (Dependency Injection):
 * 
 *    var builder = WebApplication.CreateBuilder(args);
 *    
 *    builder.Services.AddDbContext<UchatDbContext>(options =>
 *        options.UseSqlite("Data Source=uchat.db"));
 *    
 *    var app = builder.Build();
 * 
 * 
 * 2. В репозиториях (через конструктор):
 * 
 *    public class UserRepository
 *    {
 *        private readonly UchatDbContext _context;
 *        
 *        public UserRepository(UchatDbContext context)
 *        {
 *            _context = context;
 *        }
 *        
 *        public async Task<User> GetByIdAsync(int id)
 *        {
 *            return await _context.Users.FindAsync(id);
 *        }
 *    }
 * 
 * 
 * 3. Напрямую (для тестирования):
 * 
 *    var options = new DbContextOptionsBuilder<UchatDbContext>()
 *        .UseSqlite("Data Source=test.db")
 *        .Options;
 *    
 *    using var context = new UchatDbContext(options);
 *    
 *    var user = new User { Username = "test" };
 *    context.Users.Add(user);
 *    await context.SaveChangesAsync();
 * 
 * ============================================================================
 * СОЗДАНИЕ БАЗЫ ДАННЫХ И МИГРАЦИЙ
 * ============================================================================
 * 
 * После создания всех Entity моделей и DbContext нужно:
 * 
 * 1. Создать первую миграцию (migration):
 *    cd uchat_server/Uchat.Database
 *    dotnet ef migrations add InitialCreate
 *    
 *    Это создаст папку Migrations/ с файлами миграции
 * 
 * 2. Применить миграцию (создать БД):
 *    dotnet ef database update
 *    
 *    Это создаст файл uchat.db с всеми таблицами!
 * 
 * 3. При изменении моделей:
 *    - Изменяешь User.cs (добавляешь поле)
 *    - Создаешь новую миграцию: dotnet ef migrations add AddUserBio
 *    - Применяешь: dotnet ef database update
 * 
 * ============================================================================
 * ЗАДАНИЕ ДЛЯ ТЕБЯ:
 * ============================================================================
 * 
 * 1. Добавь Seed данных (начальные данные):
 *    
 *    protected override void OnModelCreating(ModelBuilder modelBuilder)
 *    {
 *        base.OnModelCreating(modelBuilder);
 *        // ... вся конфигурация ...
 *        
 *        // Seed admin пользователя
 *        modelBuilder.Entity<User>().HasData(new User
 *        {
 *            Id = 1,
 *            Username = "admin",
 *            PasswordHash = "hash",
 *            Salt = "salt",
 *            DisplayName = "Administrator",
 *            CreatedAt = DateTime.UtcNow,
 *            Status = UserStatus.Offline
 *        });
 *    }
 * 
 * 2. Добавь Soft Delete глобально:
 *    Создай Query Filter чтобы IsDeleted сообщения не показывались:
 *    
 *    modelBuilder.Entity<Message>().HasQueryFilter(m => !m.IsDeleted);
 *    
 *    Теперь все запросы автоматически будут фильтровать удаленные сообщения!
 * 
 * ============================================================================
 */
