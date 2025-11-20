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
    public DbSet<Friendship> Friendships { get; set; } = null!;
    // ========================================================================
    // DBSETS (Таблицы в базе данных)
    // ========================================================================
    // DbSet<T> - это коллекция всех записей типа T в БД
    // Каждый DbSet представляет одну таблицу
    // ========================================================================
    
    /// <summary>
    /// Таблица пользователей
    /// SQL: SELECT * FROM Users
    /// LINQ: context.Users.Where(u => u.Username == "john")
    /// </summary>
    public DbSet<User> Users { get; set; } = null!;
    
    /// <summary>
    /// Таблица сообщений
    /// </summary>
    public DbSet<Message> Messages { get; set; } = null!;
    
    /// <summary>
    /// Таблица групповых чатов
    /// </summary>
    public DbSet<ChatRoom> ChatRooms { get; set; } = null!;
    
    /// <summary>
    /// Таблица участников групповых чатов (Many-to-Many промежуточная)
    /// </summary>
    public DbSet<ChatRoomMember> ChatRoomMembers { get; set; } = null!;
    
    /// <summary>
    /// Таблица контактов пользователей
    /// </summary>
    public DbSet<Contact> Contacts { get; set; } = null!;
    
    /// <summary>
    /// Группы контактов (папки)
    /// </summary>
    public DbSet<ContactGroup> ContactGroups { get; set; } = null!;

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

            // PRIMARY KEY
            // entity.HasKey(u => u.Id) не обязательно - EF автоматически определяет свойство Id как PK
            entity.HasKey(u => u.Id);

            // ----------------------------------------------------------------
            // INDEXES (индексы для быстрого поиска)
            // ----------------------------------------------------------------
            // Без индекса: SELECT * FROM Users WHERE Username = 'john'
            //   → SQLite сканирует ВСЮ таблицу (медленно при миллионах записей)
            // С индексом: SQLite использует B-tree для мгновенного поиска
            // ----------------------------------------------------------------
            
            // UNIQUE INDEX на Username
            // Гарантирует уникальность + ускоряет поиск по Username
            entity.HasIndex(u => u.Username)
                .IsUnique()  // UNIQUE constraint
                .HasDatabaseName("IX_Users_Username");  // Имя индекса в БД
            

            // Обычный INDEX на Email (теперь NOT NULL, индексируется)
            entity.HasIndex(u => u.Email)
                .HasDatabaseName("IX_Users_Email");

            // INDEX на PhoneNumber (может быть NULL)
            entity.HasIndex(u => u.PhoneNumber)
                .HasDatabaseName("IX_Users_PhoneNumber");

            // ----------------------------------------------------------------
            // COLUMN CONSTRAINTS (ограничения на колонки)
            // ----------------------------------------------------------------
            

            entity.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(50);

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

            entity.Property(u => u.AvatarUrl)
                .HasMaxLength(500);

            entity.Property(u => u.Bio)
                .HasMaxLength(190);

            entity.Property(u => u.PhoneNumber)
                .HasMaxLength(20);

            entity.Property(u => u.BirthDate)
                .HasColumnType("date");

            entity.Property(u => u.Status)
                .HasDefaultValue(UserStatus.Offline);

            // UserLanguage хранится как строка (Code)
            entity.OwnsOne(u => u.Language, lang =>
            {
                lang.Property(l => l.Code)
                    .HasColumnName("LanguageCode")
                    .HasMaxLength(10)
                    .HasDefaultValue("en");
            });

            // DEFAULT VALUE для CreatedAt
            entity.Property(u => u.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // ----------------------------------------------------------------
            // RELATIONSHIPS (связи с другими таблицами)
            // ----------------------------------------------------------------
            
            // User -> SentMessages (One-to-Many)
            // Один пользователь может отправить много сообщений
            entity.HasMany(u => u.SentMessages)     // У User есть много SentMessages
                .WithOne(m => m.Sender)              // У Message есть один Sender
                .HasForeignKey(m => m.SenderId)      // Foreign Key в таблице Messages
                .OnDelete(DeleteBehavior.Restrict);  // Запретить удаление User, если есть сообщения
            
            // DeleteBehavior.Restrict - нельзя удалить пользователя, если у него есть сообщения
            // DeleteBehavior.Cascade - удалить пользователя → удалятся все его сообщения
            // DeleteBehavior.SetNull - удалить пользователя → SenderId = NULL (если nullable)

            // User -> ReceivedMessages (One-to-Many)
            entity.HasMany(u => u.ReceivedMessages)
                .WithOne(m => m.Receiver)
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

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
        // КОНФИГУРАЦИЯ ТАБЛИЦЫ MESSAGES
        // ====================================================================
        
        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("Messages");
            entity.HasKey(m => m.Id);

            // ----------------------------------------------------------------
            // INDEXES
            // ----------------------------------------------------------------
            // Эти индексы критически важны для производительности чата!
            // ----------------------------------------------------------------
            
            // INDEX на SenderId (часто ищем сообщения от конкретного пользователя)
            entity.HasIndex(m => m.SenderId)
                .HasDatabaseName("IX_Messages_SenderId");

            entity.HasIndex(m => m.ReceiverId)
                .HasDatabaseName("IX_Messages_ReceiverId");

            entity.HasIndex(m => m.ChatRoomId)
                .HasDatabaseName("IX_Messages_ChatRoomId");

            // INDEX на SentAt (для сортировки по времени)
            entity.HasIndex(m => m.SentAt)
                .HasDatabaseName("IX_Messages_SentAt");

            // ----------------------------------------------------------------
            // COMPOSITE INDEXES (составные индексы)
            // ----------------------------------------------------------------
            // Для ЛИЧНЫХ сообщений нужно быстро найти историю между двумя пользователями
            // SELECT * FROM Messages 
            // WHERE SenderId = 1 AND ReceiverId = 2 
            // ORDER BY SentAt DESC
            // ----------------------------------------------------------------
            
            entity.HasIndex(m => new { m.SenderId, m.ReceiverId, m.SentAt })
                .HasDatabaseName("IX_Messages_DirectChat");

            // Для ГРУППОВЫХ сообщений нужно быстро найти историю чата
            // SELECT * FROM Messages 
            // WHERE ChatRoomId = 5 
            // ORDER BY SentAt DESC
            entity.HasIndex(m => new { m.ChatRoomId, m.SentAt })
                .HasDatabaseName("IX_Messages_GroupChat");

            // ----------------------------------------------------------------
            // COLUMNS
            // ----------------------------------------------------------------
            
            entity.Property(m => m.Content)
                .IsRequired()
                .HasMaxLength(4000);  // Достаточно для большинства сообщений

            entity.Property(m => m.AttachmentUrl)
                .HasMaxLength(1000);

            entity.Property(m => m.AttachmentFileName)
                .HasMaxLength(255);

            entity.Property(m => m.SentAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // ----------------------------------------------------------------
            // SELF-REFERENCING RELATIONSHIP (сам на себя)
            // ----------------------------------------------------------------
            // Message -> ReplyToMessage
            // Сообщение может быть ответом на другое сообщение
            // ----------------------------------------------------------------
            
            entity.HasOne(m => m.ReplyToMessage)     // У Message есть один ReplyToMessage
                .WithMany(m => m.Replies)             // У Message может быть много Replies
                .HasForeignKey(m => m.ReplyToMessageId)
                .OnDelete(DeleteBehavior.SetNull);    // Удалить оригинал → ReplyToMessageId = NULL

            // ----------------------------------------------------------------
            // Message -> ChatRoom
            // ----------------------------------------------------------------
            
            entity.HasOne(m => m.ChatRoom)
                .WithMany(cr => cr.Messages)
                .HasForeignKey(m => m.ChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);  // Удалить группу → удалить все сообщения
        });

        // ====================================================================
        // КОНФИГУРАЦИЯ ТАБЛИЦЫ CHATROOMS
        // ====================================================================
        
        modelBuilder.Entity<ChatRoom>(entity =>
        {
            entity.ToTable("ChatRooms");
            entity.HasKey(cr => cr.Id);

            entity.HasIndex(cr => cr.Name)
                .HasDatabaseName("IX_ChatRooms_Name");

            entity.HasIndex(cr => cr.CreatorId)
                .HasDatabaseName("IX_ChatRooms_CreatorId");

            entity.Property(cr => cr.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(cr => cr.Description)
                .HasMaxLength(500);

            entity.Property(cr => cr.AvatarUrl)
                .HasMaxLength(500);

            entity.Property(cr => cr.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

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
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // ChatRoomMember -> InvitedBy (кто пригласил)
            entity.HasOne(crm => crm.InvitedBy)
                .WithMany()  // У User нет списка "кого я пригласил"
                .HasForeignKey(crm => crm.InvitedById)
                .OnDelete(DeleteBehavior.SetNull);  // Удалить пригласившего → InvitedById = NULL
        });

        // ====================================================================
        // КОНФИГУРАЦИЯ ТАБЛИЦЫ CONTACTS
        // ====================================================================
        
        modelBuilder.Entity<Contact>(entity =>
        {
            entity.Property(c => c.Notes)
                .HasMaxLength(500);

            entity.Property(c => c.NotificationsEnabled)
                .HasDefaultValue(true);

            entity.Property(c => c.IsFavorite)
                .HasDefaultValue(false);
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

            entity.Property(c => c.CustomRingtone)
                .HasMaxLength(255);

            entity.Property(c => c.MessageCount)
                .HasDefaultValue(0);

            entity.Property(c => c.ShowTypingIndicator)
                .HasDefaultValue(true);

            entity.Property(c => c.LastMessageAt)
                .HasColumnType("TEXT");

            entity.Property(c => c.AddedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Contact -> ContactUser
            entity.HasOne(c => c.ContactUser)
                .WithMany()  // У User нет списка "кто меня добавил в контакты"
                .HasForeignKey(c => c.ContactUserId)
                .OnDelete(DeleteBehavior.Cascade);  // Удалить user → удалить контакты с ним

            // Contact -> ContactGroup (опционально)
            entity.HasOne(c => c.Group)
                .WithMany(g => g.Contacts)
                .HasForeignKey(c => c.GroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Конфигурация ContactGroups
        modelBuilder.Entity<ContactGroup>(entity =>
        {
            entity.ToTable("ContactGroups");
            entity.HasKey(g => g.Id);

            entity.Property(g => g.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(g => g.Color)
                .HasMaxLength(50);

            entity.HasIndex(g => new { g.OwnerId, g.Name })
                .IsUnique()
                .HasDatabaseName("IX_ContactGroups_Owner_Name");

            entity.HasOne(g => g.Owner)
                .WithMany()
                .HasForeignKey(g => g.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        // Seed admin пользователя
                // ====================================================================
                // КОНФИГУРАЦИЯ ТАБЛИЦЫ FRIENDSHIPS
                // ====================================================================
                modelBuilder.Entity<Friendship>(entity =>
                {
                    entity.ToTable("Friendships");
                    entity.HasKey(f => f.Id);

                    // Один пользователь не может отправить несколько запросов одному и тому же человеку
                    entity.HasIndex(f => new { f.SenderId, f.ReceiverId })
                        .IsUnique()
                        .HasDatabaseName("IX_Friendships_Sender_Receiver");

                    // Индексы для быстрого поиска
                    entity.HasIndex(f => f.SenderId).HasDatabaseName("IX_Friendships_SenderId");
                    entity.HasIndex(f => f.ReceiverId).HasDatabaseName("IX_Friendships_ReceiverId");

                    entity.Property(f => f.Status)
                        .HasDefaultValue(FriendshipStatus.Pending);

                    entity.Property(f => f.CreatedAt)
                        .HasDefaultValueSql("CURRENT_TIMESTAMP");
                    entity.Property(f => f.UpdatedAt)
                        .HasDefaultValueSql("CURRENT_TIMESTAMP");

                    // Навигационные свойства
                    entity.HasOne(f => f.Sender)
                        .WithMany()
                        .HasForeignKey(f => f.SenderId)
                        .OnDelete(DeleteBehavior.Restrict);

                    entity.HasOne(f => f.Receiver)
                        .WithMany()
                        .HasForeignKey(f => f.ReceiverId)
                        .OnDelete(DeleteBehavior.Restrict);
                });
        modelBuilder.Entity<User>().HasData(new User
        {
            Id = 1,
            Username = "admin",
            PasswordHash = "hash",
            Salt = "salt",
            DisplayName = "Administrator",
            CreatedAt = DateTime.UtcNow,
            Status = UserStatus.Offline
        });

        // Soft Delete: фильтруем удалённые сообщения
        modelBuilder.Entity<Message>().HasQueryFilter(m => !m.IsDeleted);
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
