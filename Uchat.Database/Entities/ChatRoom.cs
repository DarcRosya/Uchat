/*
 * ============================================================================
 * ENTITY MODEL: CHATROOM (Групповой чат)
 * ============================================================================
 * 
 * Это модель для групповых чатов (как группы в Telegram/WhatsApp).
 * 
 * ВАЖНО ПОНЯТЬ:
 * - Личные сообщения: User -> Message -> User (напрямую)
 * - Групповые сообщения: User -> Message -> ChatRoom <- ChatRoomMembers
 * 
 * ChatRoom это "контейнер" для группового общения.
 * Связь с участниками через промежуточную таблицу ChatRoomMembers.
 * 
 * ============================================================================
 */

namespace Uchat.Database.Entities;


public class ChatRoom
{
    // ========================================================================
    // PRIMARY KEY
    // ========================================================================

    public int Id { get; set; }

    // ========================================================================
    // ОСНОВНАЯ ИНФОРМАЦИЯ О ГРУППЕ
    // ========================================================================

    public string Name { get; set; } = string.Empty;
    
    /// Описание группы/канала
    /// 
    /// NULL для:
    ///   - DirectMessage (личные чаты не имеют описания)
    ///   - Topic (топики не имеют описания, только имя)
    /// 
    /// Используется для:
    ///   - Private/Public групп
    ///   - Channels
    public string? Description { get; set; }
    
    /// URL аватара группы (картинка/иконка)
    /// В БД: VARCHAR(500) NULL
    /// 
    /// Примеры:
    ///   - "/uploads/groups/avatar_42.png"
    ///   - "https://cdn.example.com/group_icons/team.jpg"
    public string? IconUrl { get; set; }

    // ========================================================================
    // СОЗДАТЕЛЬ И МЕТАДАННЫЕ
    // ========================================================================

    /// ID родительской группы (для топиков)
    /// 
    /// NULL - обычная группа/личный чат
    /// NOT NULL - топик внутри группы
    /// 
    /// Примеры:
    ///   - Группа "Dev Team": ParentChatRoomId = NULL, Type = Private
    ///   - Топик "General": ParentChatRoomId = 1, Type = Topic
    public int? ParentChatRoomId { get; set; }
    
    /// Родительская группа (Navigation Property)
    /// 
    /// ЭТО НЕ КОЛОНКА В БД! Это объект для загрузки.
    /// 
    /// Использование:
    ///   var topic = await context.ChatRooms
    ///       .Include(cr => cr.ParentChatRoom)  // ← Явная загрузка
    ///       .FirstAsync(cr => cr.Id == 2);
    ///   
    ///   Console.WriteLine($"Топик '{topic.Name}' в группе '{topic.ParentChatRoom.Name}'");
    /// 
    /// БЕЗ .Include() это свойство будет NULL!
    public ChatRoom? ParentChatRoom { get; set; }

    /// ID пользователя, который создал эту группу/топик
    /// Создатель обычно автоматически получает роль Owner
    public int CreatorId { get; set; }
    public DateTime CreatedAt { get; set; }

    // ========================================================================
    // НАСТРОЙКИ ГРУППЫ
    // ========================================================================

    /// Тип чата/группы/канала
    /// 
    /// - DirectMessage: личный чат 1-on-1
    /// - Private: приватная группа (только по приглашению)
    /// - Public: публичная группа (можно найти через поиск и присоединиться)
    /// - Channel: канал (только админы пишут, остальные читают)
    /// - Topic: топик внутри группы/канала
    public ChatRoomType Type { get; set; }
    
    /// Максимальное количество участников
    /// 
    /// NULL - без ограничений
    /// DirectMessage - всегда 2 участника (валидация на уровне приложения)
    /// 
    /// Примеры:
    ///   - DirectMessage: игнорируется (всегда 2)
    ///   - Private группа: 200
    ///   - Channel: 100000
    public int? MaxMembers { get; set; }

    // ========================================================================
    // GLOBAL DEFAULT PERMISSIONS (Level 1: для всех обычных участников)
    // ========================================================================
    // Admins bypass these restrictions (Level 2)
    // Individual exceptions via ChatRoomMemberPermissions (Level 3)
    // ========================================================================

    /// <summary>
    /// Default: can members send text messages
    /// NULL = use type default (DM: true, Channel: false, Group: true)
    /// Admins always can send (unless muted)
    /// </summary>
    public bool? DefaultCanSendMessages { get; set; }

    /// Default: can members send photos
    /// NULL = true
    /// Admins always can send
    public bool? DefaultCanSendPhotos { get; set; }
    
    /// <summary>
    /// Default: can members send videos
    /// NULL = true
    /// </summary>
    public bool? DefaultCanSendVideos { get; set; }
    
    /// <summary>
    /// Default: can members send stickers and GIFs
    /// NULL = true
    /// </summary>
    public bool? DefaultCanSendStickers { get; set; }
    
    /// <summary>
    /// Default: can members send music/audio files
    /// NULL = true
    /// </summary>
    public bool? DefaultCanSendMusic { get; set; }
    
    /// <summary>
    /// Default: can members send files and documents
    /// NULL = true
    /// </summary>
    public bool? DefaultCanSendFiles { get; set; }

    /// <summary>
    /// Default: can members invite other users
    /// NULL = use type default (DM: false, Private: false, Public: true)
    /// Admins can be restricted via individual permissions
    /// </summary>
    public bool? DefaultCanInviteUsers { get; set; }
    
    /// <summary>
    /// Default: can members pin messages
    /// NULL = false
    /// Admins can be restricted via individual permissions
    /// </summary>
    public bool? DefaultCanPinMessages { get; set; }
    
    /// <summary>
    /// Default: can members customize group (change name, description, icon)
    /// NULL = false (only admins by default)
    /// Admins can be restricted via individual permissions
    /// </summary>
    public bool? DefaultCanCustomizeGroup { get; set; }

    /// Медленный режим (Slow Mode)
    /// 
    /// NULL - отключен
    /// >0 - пользователь может писать 1 сообщение в N секунд
    /// 
    /// ВАЖНО: админы игнорируют это ограничение
    public int? SlowModeSeconds { get; set; }

    // ========================================================================
    // СТАТИСТИКА (обновляется автоматически при новых сообщениях)
    // ========================================================================

    /// Общее количество сообщений в чате
    /// 
    /// Обновляется при каждом новом сообщении через MessageRepository:
    ///   await mongoRepo.SendMessageAsync(...);
    ///   await sqliteContext.ChatRooms
    ///       .Where(cr => cr.Id == chatId)
    ///       .ExecuteUpdateAsync(cr => cr.SetProperty(
    ///           x => x.TotalMessagesCount,
    ///           x => x.TotalMessagesCount + 1
    ///       ));
    /// 
    /// Можно использовать для:
    ///   - Отображения активности группы
    ///   - Ранжирования популярных групп
    public int TotalMessagesCount { get; set; }

    /// Время последнего сообщения в чате
    /// 
    /// NULL - сообщений ещё не было
    /// 
    /// Обновляется при каждом новом сообщении:
    ///   LastActivityAt = DateTime.UtcNow
    /// 
    /// Используется для:
    ///   - Сортировки чатов по активности
    ///   - Показа "последний раз активен X минут назад"
    public DateTime? LastActivityAt { get; set; }

    // ========================================================================
    // НАВИГАЦИОННЫЕ СВОЙСТВА
    // ========================================================================

    /// Создатель группы
    /// 
    /// Navigation property для CreatorId
    /// 
    /// Использование:
    ///   var chatRoom = await context.ChatRooms
    ///       .Include(cr => cr.Creator)
    ///       .FirstAsync(cr => cr.Id == 5);
    ///   
    ///   Console.WriteLine($"Создатель: {chatRoom.Creator.Username}");
    public User Creator { get; set; } = null!;
    
    // Связь: LiteDbMessage.ChatId == ChatRoom.Id
    // Для получения сообщений используй MessageRepository:
    //   var messages = await mongoRepo.GetChatMessagesAsync(chatRoom.Id, limit: 50);
    
    /// Все участники этой группы
    /// 
    /// Связь: ChatRoom (1) -> ChatRoomMember (Many) -> User (1)
    /// Это Many-to-Many через промежуточную таблицу
    /// 
    /// ПОЧЕМУ ЧЕРЕЗ ПРОМЕЖУТОЧНУЮ ТАБЛИЦУ?
    /// Потому что нам нужно хранить дополнительную информацию о членстве:
    /// - Когда присоединился
    /// - Роль (админ, модератор, обычный участник)
    /// - Кто пригласил
    /// - Заблокирован ли в группе
    /// 
    /// Использование:
    ///   var chatRoom = await context.ChatRooms
    ///       .Include(cr => cr.Members)
    ///           .ThenInclude(m => m.User)  // Загружаем самих пользователей
    ///       .FirstAsync(cr => cr.Id == 5);
    ///   
    ///   foreach (var member in chatRoom.Members) {
    ///       Console.WriteLine($"{member.User.Username} - {member.Role}");
    ///   }
    public ICollection<ChatRoomMember> Members { get; set; } = new List<ChatRoomMember>();
    
    /// Дочерние топики (Navigation Property)
    /// 
    /// Коллекция топиков, у которых ParentChatRoomId == этот ChatRoom.Id
    /// 
    /// ЭТО НЕ КОЛОНКА В БД! Это обратная навигация.
    /// 
    /// Использование:
    ///   var group = await context.ChatRooms
    ///       .Include(cr => cr.Topics)  // ← Загрузить дочерние топики
    ///       .FirstAsync(cr => cr.Id == 1);
    ///   
    ///   foreach (var topic in group.Topics) {
    ///       Console.WriteLine($"- {topic.Name}");
    ///   }
    /// 
    /// ВАЖНО:
    /// - У обычных групп здесь будут топики
    /// - У топиков эта коллекция будет пустой (топик не может содержать топики)
    /// - У личных чатов (DirectMessage) эта коллекция пустая
    public ICollection<ChatRoom> Topics { get; set; } = new List<ChatRoom>();
}


public enum ChatRoomType
{
    /// Личный чат между двумя пользователями (1-on-1)
    /// 
    /// Особенности:
    ///   - Всегда ровно 2 участника
    ///   - Нельзя приглашать других
    ///   - Нет админов (оба равноправны)
    ///   - Настройки разрешений игнорируются
    DirectMessage = 0,
    
    /// Публичная группа
    /// 
    /// Особенности:
    ///   - Можно найти через поиск
    ///   - Любой может присоединиться
    ///   - Настройки разрешений работают
    Public = 1,
    
    /// Приватная группа
    /// 
    /// Особенности:
    ///   - Нельзя найти через поиск
    ///   - Только по приглашению
    ///   - Настройки разрешений работают
    Private = 2,
    
    /// Топик внутри группы/канала
    /// 
    /// Особенности:
    ///   - ParentChatRoomId != NULL
    ///   - Наследует участников от родительской группы
    ///   - Наследует настройки (НЕЛЬЗЯ переопределить)
    ///   - AllowMembers* поля ИГНОРИРУЮТСЯ (всегда NULL)
    Topic = 3,
    
    /// Канал (как в Telegram)
    /// 
    /// Особенности:
    ///   - Только админы могут писать сообщения
    ///   - Участники только читают (подписчики)
    ///   - AllowMembersToSendMessages всегда false
    Channel = 4
}

/*
 * ============================================================================
 * ЛОГИКА РАБОТЫ С РАЗНЫМИ ТИПАМИ ЧАТОВ
 * ============================================================================
 * 
 * 1. DirectMessage (личный чат):
 *    ────────────────────────────────
 *    Создание:
 *      var chat = new ChatRoom {
 *          Type = ChatRoomType.DirectMessage,
 *          CreatorId = user1.Id
 *      };
 *      context.ChatRoomMembers.Add(new ChatRoomMember { 
 *          ChatRoomId = chat.Id, UserId = user1.Id, Role = MemberRole.Member 
 *      });
 *      context.ChatRoomMembers.Add(new ChatRoomMember { 
 *          ChatRoomId = chat.Id, UserId = user2.Id, Role = MemberRole.Member 
 *      });
 *    
 *    Валидация:
 *      - Всегда ровно 2 участника
 *      - Нельзя добавить третьего
 *      - Если один выходит → чат удаляется
 *    
 *    Поиск существующего:
 *      var existingChat = await context.ChatRooms
 *          .Where(cr => cr.Type == ChatRoomType.DirectMessage)
 *          .Where(cr => cr.Members.Count == 2)
 *          .Where(cr => cr.Members.Any(m => m.UserId == user1Id))
 *          .Where(cr => cr.Members.Any(m => m.UserId == user2Id))
 *          .FirstOrDefaultAsync();
 * 
 * 
 * 2. Private/Public Group (группы):
 *    ────────────────────────────────
 *    Создание:
 *      var group = new ChatRoom {
 *          Type = ChatRoomType.Private,
 *          Name = "Dev Team",
 *          CreatorId = userId,
 *          AllowMembersToInvite = false,
 *          AllowMembersToSendMessages = true,
 *          SlowModeSeconds = null
 *      };
 *    
 *    Создатель автоматически становится Owner:
 *      context.ChatRoomMembers.Add(new ChatRoomMember {
 *          ChatRoomId = group.Id,
 *          UserId = userId,
 *          Role = MemberRole.Owner
 *      });
 * 
 * 
 * 3. Channel (канал):
 *    ────────────────────────────────
 *    Создание:
 *      var channel = new ChatRoom {
 *          Type = ChatRoomType.Channel,
 *          Name = "Tech News",
 *          AllowMembersToSendMessages = false,  // ← ВАЖНО!
 *          AllowMembersToSendMedia = false
 *      };
 *    
 *    Проверка прав при отправке:
 *      if (chatRoom.Type == ChatRoomType.Channel) {
 *          var member = await GetMemberAsync(chatRoomId, userId);
 *          if (member.Role != MemberRole.Admin && member.Role != MemberRole.Owner) {
 *              throw new ForbiddenException("Only admins can post in channels");
 *          }
 *      }
 * 
 * 
 * 4. Topic (топик):
 *    ────────────────────────────────
 *    Создание:
 *      var topic = new ChatRoom {
 *          Type = ChatRoomType.Topic,
 *          Name = "General Discussion",
 *          ParentChatRoomId = parentGroupId,  // ← Связь с группой
 *          CreatorId = userId,
 *          // ВАЖНО: AllowMembers* поля оставляем NULL (наследуются от родителя)
 *          // Description не используется для топиков (всегда NULL)
 *      };
 *    
 *    Получение участников (наследуются от родителя):
 *      public async Task<List<User>> GetTopicMembersAsync(int topicId) {
 *          var topic = await context.ChatRooms
 *              .Include(cr => cr.ParentChatRoom)
 *                  .ThenInclude(p => p.Members)
 *                      .ThenInclude(m => m.User)
 *              .FirstAsync(cr => cr.Id == topicId);
 *          
 *          if (topic.Type != ChatRoomType.Topic || topic.ParentChatRoom == null) {
 *              throw new InvalidOperationException("Not a topic!");
 *          }
 *          
 *          return topic.ParentChatRoom.Members
 *              .Select(m => m.User)
 *              .ToList();
 *      }
 *    
 *    Проверка прав (наследуются от родителя):
 *      // Топик ВСЕГДА использует настройки родителя
 *      var effectiveSettings = topic.ParentChatRoom?.GetEffectiveAllowMembersToSendMessages() 
 *          ?? true;  // По умолчанию разрешено
 * 
 * ============================================================================
 * ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ
 * ============================================================================
 * 
 * 1. Создать личный чат:
 *    ──────────────────────
 *    public async Task<ChatRoom> GetOrCreateDirectChatAsync(int user1Id, int user2Id) {
 *        // Проверить существующий чат
 *        var existing = await context.ChatRooms
 *            .Include(cr => cr.Members)
 *            .Where(cr => cr.Type == ChatRoomType.DirectMessage)
 *            .Where(cr => cr.Members.Any(m => m.UserId == user1Id))
 *            .Where(cr => cr.Members.Any(m => m.UserId == user2Id))
 *            .FirstOrDefaultAsync();
 *        
 *        if (existing != null) return existing;
 *        
 *        // Создать новый
 *        var chat = new ChatRoom {
 *            Type = ChatRoomType.DirectMessage,
 *            CreatorId = user1Id,
 *            CreatedAt = DateTime.UtcNow
 *        };
 *        context.ChatRooms.Add(chat);
 *        await context.SaveChangesAsync();
 *        
 *        // Добавить участников
 *        context.ChatRoomMembers.AddRange(
 *            new ChatRoomMember { ChatRoomId = chat.Id, UserId = user1Id, Role = MemberRole.Member },
 *            new ChatRoomMember { ChatRoomId = chat.Id, UserId = user2Id, Role = MemberRole.Member }
 *        );
 *        await context.SaveChangesAsync();
 *        
 *        return chat;
 *    }
 * 
 * 
 * 2. Проверить права на отправку сообщения:
 *    ──────────────────────────────────────
 *    public async Task<bool> CanSendMessageAsync(int chatRoomId, int userId) {
 *        var chatRoom = await context.ChatRooms
 *            .Include(cr => cr.ParentChatRoom)  // Для топиков
 *            .FirstAsync(cr => cr.Id == chatRoomId);
 *        
 *        var member = await context.ChatRoomMembers
 *            .FirstOrDefaultAsync(m => m.ChatRoomId == chatRoomId && m.UserId == userId);
 *        
 *        if (member == null) return false;  // Не участник
 *        
 *        // Админы всегда могут писать
 *        if (member.Role == MemberRole.Admin || member.Role == MemberRole.Owner) {
 *            return true;
 *        }
 *        
 *        // DirectMessage - оба могут писать
 *        if (chatRoom.Type == ChatRoomType.DirectMessage) {
 *            return true;
 *        }
 *        
 *        // Topic - проверить настройки родителя
 *        if (chatRoom.Type == ChatRoomType.Topic) {
 *            var allowMessages = chatRoom.AllowMembersToSendMessages 
 *                ?? chatRoom.ParentChatRoom?.AllowMembersToSendMessages 
 *                ?? true;
 *            return allowMessages;
 *        }
 *        
 *        // Channel - только админы
 *        if (chatRoom.Type == ChatRoomType.Channel) {
 *            return false;
 *        }
 *        
 *        // Group - проверить настройки
 *        return chatRoom.AllowMembersToSendMessages ?? true;
 *    }
 * 
 * 
 * 3. Обновить статистику при новом сообщении:
 *    ──────────────────────────────────────────
 *    public async Task OnMessageSentAsync(int chatRoomId) {
 *        await context.ChatRooms
 *            .Where(cr => cr.Id == chatRoomId)
 *            .ExecuteUpdateAsync(cr => cr
 *                .SetProperty(x => x.TotalMessagesCount, x => x.TotalMessagesCount + 1)
 *                .SetProperty(x => x.LastActivityAt, DateTime.UtcNow)
 *            );
 *    }
 * 
 * ============================================================================
 */
