# 🏗️ Архитектура ChatRoom - Полное объяснение

## 🎯 Главная идея: ОДИН класс для ВСЕХ чатов

### ✅ Твоё решение - ПРАВИЛЬНОЕ!

Ты абсолютно верно выбрал подход: **хранить ВСЁ через ChatRoom**.

```
                        ┌─────────────────┐
                        │   ChatRoom      │
                        ├─────────────────┤
                        │ + Id            │
                        │ + Type (enum)   │◄────┐
                        │ + Name          │     │
                        │ + ParentId      │─────┘ Self-reference
                        │ + Settings...   │      (для топиков)
                        └─────────────────┘
                                ▲
                                │
                ┌───────────────┼───────────────┬───────────────┐
                │               │               │               │
        DirectMessage       Private         Public          Channel
        (2 участника)      (группа)        (группа)      (подписка)
```

---

## 📊 Сравнение подходов

### ❌ Плохой подход (отдельные таблицы):

```sql
CREATE TABLE DirectChats (
    Id INT PRIMARY KEY,
    User1Id INT,
    User2Id INT
);

CREATE TABLE Groups (
    Id INT PRIMARY KEY,
    Name VARCHAR(100),
    Type INT  -- Public/Private
);

CREATE TABLE Channels (
    Id INT PRIMARY KEY,
    Name VARCHAR(100),
    SubscribersCount INT
);

CREATE TABLE Topics (
    Id INT PRIMARY KEY,
    GroupId INT,  -- FK to Groups
    Name VARCHAR(100)
);
```

**Проблемы:**
- 🔴 4 разные таблицы
- 🔴 4 разных репозитория
- 🔴 Дублирование кода (CRUD для каждой таблицы)
- 🔴 Сложно добавлять новые типы
- 🔴 Messages должны ссылаться на разные таблицы

---

### ✅ Хороший подход (полиморфизм через Type):

```sql
CREATE TABLE ChatRooms (
    Id INT PRIMARY KEY,
    Type INT,  -- DirectMessage=0, Private=1, Public=2, Topic=3, Channel=4
    Name VARCHAR(100) NULL,
    ParentChatRoomId INT NULL,  -- FOREIGN KEY REFERENCES ChatRooms(Id)
    
    -- Настройки (nullable, используются по необходимости)
    AllowMembersToInvite BIT NULL,
    AllowMembersToSendMessages BIT NULL,
    AllowMembersToSendMedia BIT NULL,
    SlowModeSeconds INT NULL,
    
    -- Статистика
    TotalMessagesCount INT DEFAULT 0,
    LastActivityAt DATETIME NULL,
    
    -- Метаданные
    CreatorId INT NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

**Преимущества:**
- ✅ Одна таблица
- ✅ Один репозиторий
- ✅ Переиспользование кода
- ✅ Легко добавлять новые типы (просто enum)
- ✅ Messages ссылаются на одну таблицу (ChatRooms)

---

## 🧩 Детальная структура

### 1. ChatRoom (основной класс)

```csharp
public class ChatRoom {
    // ═══════════════════════════════════════════════════════
    // БАЗОВЫЕ ПОЛЯ (для всех типов)
    // ═══════════════════════════════════════════════════════
    public int Id { get; set; }
    public ChatRoomType Type { get; set; }  // ← Определяет поведение
    public string Name { get; set; }        // NULL для DirectMessage
    public int CreatorId { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // ═══════════════════════════════════════════════════════
    // ДЛЯ ТОПИКОВ (Self-referencing relationship)
    // ═══════════════════════════════════════════════════════
    public int? ParentChatRoomId { get; set; }          // FK (колонка в БД)
    public ChatRoom? ParentChatRoom { get; set; }       // Navigation (объект)
    public ICollection<ChatRoom> Topics { get; set; }   // Обратная навигация
    
    // ═══════════════════════════════════════════════════════
    // НАСТРОЙКИ (только для групп/каналов, NULL для личных)
    // ═══════════════════════════════════════════════════════
    public bool? AllowMembersToInvite { get; set; }
    public bool? AllowMembersToSendMessages { get; set; }
    public bool? AllowMembersToSendMedia { get; set; }
    public int? SlowModeSeconds { get; set; }
    public int? MaxMembers { get; set; }
    
    // ═══════════════════════════════════════════════════════
    // СТАТИСТИКА (обновляется автоматически)
    // ═══════════════════════════════════════════════════════
    public int TotalMessagesCount { get; set; }     // Счётчик сообщений
    public DateTime? LastActivityAt { get; set; }   // Последнее сообщение
    
    // ═══════════════════════════════════════════════════════
    // НАВИГАЦИЯ (связи с другими таблицами)
    // ═══════════════════════════════════════════════════════
    public User Creator { get; set; }
    public ICollection<ChatRoomMember> Members { get; set; }
}
```

---

## 🔑 Self-referencing relationship

### Это КЛЮЧЕВАЯ концепция для топиков!

```
ChatRooms Table:
┌────┬─────────────────┬──────────────────┬──────┐
│ Id │ Name            │ ParentChatRoomId │ Type │
├────┼─────────────────┼──────────────────┼──────┤
│ 1  │ Dev Team        │ NULL             │ 2    │  ← Обычная группа
│ 2  │ General         │ 1                │ 3    │  ← Топик группы #1
│ 3  │ Announcements   │ 1                │ 3    │  ← Топик группы #1
│ 4  │ Off-Topic       │ 1                │ 3    │  ← Топик группы #1
└────┴─────────────────┴──────────────────┴──────┘
         ▲                     │
         └─────────────────────┘ FK ссылается на ту же таблицу!
```

**Как это работает в памяти:**

```csharp
var group = await context.ChatRooms
    .Include(cr => cr.Topics)  // ← Загрузить дочерние топики
    .FirstAsync(cr => cr.Id == 1);

// Теперь:
group.Id = 1
group.Name = "Dev Team"
group.ParentChatRoomId = null
group.Topics = [
    { Id=2, Name="General", ParentChatRoomId=1 },
    { Id=3, Name="Announcements", ParentChatRoomId=1 },
    { Id=4, Name="Off-Topic", ParentChatRoomId=1 }
]
```

**Обратная навигация:**

```csharp
var topic = await context.ChatRooms
    .Include(cr => cr.ParentChatRoom)  // ← Загрузить родителя
    .FirstAsync(cr => cr.Id == 2);

// Теперь:
topic.Id = 2
topic.Name = "General"
topic.ParentChatRoomId = 1
topic.ParentChatRoom = { Id=1, Name="Dev Team", ... }
```

---

## 🎭 Поведение разных типов

### DirectMessage (Type = 0)

```csharp
DirectMessage {
    Type = DirectMessage,
    Name = null,  // ← Имя генерируется на клиенте
    ParentChatRoomId = null,
    
    // Настройки ИГНОРИРУЮТСЯ:
    AllowMembersToInvite = null,      // → эффективное: false
    AllowMembersToSendMessages = null, // → эффективное: true
    AllowMembersToSendMedia = null,   // → эффективное: true
    SlowModeSeconds = null,           // → эффективное: null
    
    Members.Count = 2  // ← ВСЕГДА ровно 2!
}

// Проверка:
chatRoom.GetEffectiveAllowMembersToSendMessages()  // → true (всегда)
chatRoom.IsDirectMessage()  // → true
```

---

### Private/Public Group (Type = 1 или 2)

```csharp
Group {
    Type = Private,
    Name = "Dev Team",
    ParentChatRoomId = null,
    
    // Настройки РАБОТАЮТ:
    AllowMembersToInvite = false,
    AllowMembersToSendMessages = true,
    AllowMembersToSendMedia = true,
    SlowModeSeconds = 5,
    
    Members.Count > 2  // Много участников
}

// Проверка:
chatRoom.GetEffectiveAllowMembersToSendMessages()  // → true
chatRoom.IsGroup()  // → true
```

---

### Channel (Type = 4)

```csharp
Channel {
    Type = Channel,
    Name = "Tech News",
    ParentChatRoomId = null,
    
    // КЛЮЧЕВАЯ настройка:
    AllowMembersToSendMessages = false,  // ← Только админы пишут!
    AllowMembersToSendMedia = false,
    
    Members.Count > 2  // Много подписчиков
}

// Проверка:
chatRoom.GetEffectiveAllowMembersToSendMessages()  // → false
chatRoom.IsChannel()  // → true

// В коде:
if (chatRoom.IsChannel() && member.Role != ChatRoomRole.Admin) {
    throw new ForbiddenException("Only admins can post");
}
```

---

### Topic (Type = 3)

```csharp
Topic {
    Type = Topic,
    Name = "General Discussion",
    ParentChatRoomId = 1,  // ← Связь с группой!
    
    // Настройки НАСЛЕДУЮТСЯ:
    AllowMembersToInvite = null,      // → берётся от родителя
    AllowMembersToSendMessages = null, // → берётся от родителя
    
    // ИЛИ переопределяются:
    AllowMembersToSendMessages = false  // → только админы (даже если в группе все могут)
}

// Проверка:
chatRoom.GetEffectiveAllowMembersToSendMessages()
// → сначала проверяет chatRoom.AllowMembersToSendMessages
// → если null, берёт ParentChatRoom.AllowMembersToSendMessages
// → если и там null, возвращает default (true)

chatRoom.IsTopic()  // → true
chatRoom.CanHaveTopics()  // → false (топик не может содержать топики)

// Участники:
var members = await context.GetTopicMembersAsync(topicId);
// → берутся из ParentChatRoom.Members, а НЕ из своих Members!
```

---

## 🧬 Наследование участников для топиков

### ВАЖНО ПОНЯТЬ!

**Топики НЕ имеют своих участников в таблице ChatRoomMembers!**

Они **наследуют** участников от родительской группы:

```
┌──────────────────────┐
│ ChatRoom #1          │
│ Type: Private        │
│ Name: "Dev Team"     │
└──────────────────────┘
         ▲
         │ Members:
         ├─ User #10 (Admin)
         ├─ User #20 (Member)
         └─ User #30 (Member)
         
         │
         ├─► ┌──────────────────────┐
         │   │ ChatRoom #2          │
         │   │ Type: Topic          │
         │   │ Name: "General"      │
         │   │ ParentId: 1          │
         │   └──────────────────────┘
         │        Участники: User #10, #20, #30 (наследуются!)
         │
         └─► ┌──────────────────────┐
             │ ChatRoom #3          │
             │ Type: Topic          │
             │ Name: "Announcements"│
             │ ParentId: 1          │
             └──────────────────────┘
                  Участники: User #10, #20, #30 (наследуются!)
```

**В БД:**

```sql
-- ChatRoomMembers table:
┌────┬───────────┬────────┬──────┐
│ Id │ ChatRoomId│ UserId │ Role │
├────┼───────────┼────────┼──────┤
│ 1  │ 1         │ 10     │ 1    │  ← Участник группы #1
│ 2  │ 1         │ 20     │ 0    │  ← Участник группы #1
│ 3  │ 1         │ 30     │ 0    │  ← Участник группы #1
└────┴───────────┴────────┴──────┘

НЕТ записей для ChatRoomId = 2 или 3 (топики)!
```

**Получение участников топика:**

```csharp
// ✅ ПРАВИЛЬНО:
var members = await context.GetTopicMembersAsync(topicId);
// → Загружает topic.ParentChatRoom.Members

// ❌ НЕПРАВИЛЬНО:
var members = await context.ChatRoomMembers
    .Where(m => m.ChatRoomId == topicId)
    .ToListAsync();
// → Вернёт пустой список!
```

---

## 🎨 Extension Methods

### Зачем нужны?

Extension methods делают код **чище** и **понятнее**:

```csharp
// ❌ БЕЗ extension methods (длинно и сложно):
bool canWrite;
if (chatRoom.Type == ChatRoomType.DirectMessage) {
    canWrite = true;
} else if (chatRoom.Type == ChatRoomType.Topic && chatRoom.ParentChatRoom != null) {
    canWrite = chatRoom.AllowMembersToSendMessages 
        ?? chatRoom.ParentChatRoom.AllowMembersToSendMessages 
        ?? true;
} else if (chatRoom.Type == ChatRoomType.Channel) {
    canWrite = false;
} else {
    canWrite = chatRoom.AllowMembersToSendMessages ?? true;
}

// ✅ С extension methods (коротко и ясно):
bool canWrite = chatRoom.GetEffectiveAllowMembersToSendMessages();
```

### Доступные методы:

**ChatRoomExtensions (для объектов):**
```csharp
chatRoom.GetEffectiveAllowMembersToInvite()
chatRoom.GetEffectiveAllowMembersToSendMessages()
chatRoom.GetEffectiveAllowMembersToSendMedia()
chatRoom.GetEffectiveSlowModeSeconds()

chatRoom.IsDirectMessage()   // Type == DirectMessage
chatRoom.IsTopic()           // Type == Topic && ParentChatRoomId != null
chatRoom.IsChannel()         // Type == Channel
chatRoom.IsGroup()           // Type == Private || Public
chatRoom.CanHaveTopics()     // Может ли содержать топики
```

**ChatRoomQueryExtensions (для DbContext):**
```csharp
await context.GetTopicMembersAsync(topicId)
await context.CanUserSendMessageAsync(chatRoomId, userId)
await context.CanUserSendMediaAsync(chatRoomId, userId)
await context.GetOrCreateDirectChatAsync(user1Id, user2Id)
await context.UpdateChatStatisticsAsync(chatRoomId)
```

---

## 🔐 Логика проверки прав

### Приоритет проверок:

```
1. Является ли участником?
   ↓ НЕТ → Отказать
   ↓ ДА
   
2. Является ли админом/владельцем?
   ↓ ДА → Разрешить (админы могут всё)
   ↓ НЕТ
   
3. Какой тип чата?
   ├─ DirectMessage → Разрешить (оба могут)
   ├─ Channel → Отказать (только админы)
   ├─ Topic → Проверить настройки (с наследованием)
   └─ Group → Проверить настройки
   
4. Проверить эффективные настройки
   ↓
   
5. Slow Mode (если включён)
   ↓
   
6. РАЗРЕШИТЬ
```

### Пример реализации:

```csharp
public async Task<bool> CanSendAsync(int chatRoomId, int userId) {
    // 1. Загрузить чат и участника
    var chatRoom = await context.ChatRooms
        .Include(cr => cr.ParentChatRoom)
        .FirstAsync(cr => cr.Id == chatRoomId);
    
    var member = await context.ChatRoomMembers
        .FirstOrDefaultAsync(m => m.ChatRoomId == chatRoomId && m.UserId == userId);
    
    if (member == null) return false;  // Не участник
    
    // 2. Админы могут всегда
    if (member.Role == ChatRoomRole.Admin || member.Role == ChatRoomRole.Owner)
        return true;
    
    // 3. Проверить настройки с учётом типа и наследования
    return chatRoom.GetEffectiveAllowMembersToSendMessages();
}
```

---

## 📈 Статистика

### Зачем TotalMessagesCount и LastActivityAt?

**TotalMessagesCount:**
- Показать активность группы ("1.2K messages")
- Ранжирование популярных групп
- Метрики для аналитики

**LastActivityAt:**
- Сортировка чатов (сначала активные)
- Показать "Last active 5 minutes ago"
- Автоматическая архивация неактивных чатов

### Обновление:

```csharp
// После каждого сообщения в MongoDB:
await context.ChatRooms
    .Where(cr => cr.Id == chatRoomId)
    .ExecuteUpdateAsync(cr => cr
        .SetProperty(x => x.TotalMessagesCount, x => x.TotalMessagesCount + 1)
        .SetProperty(x => x.LastActivityAt, DateTime.UtcNow)
    );

// ИЛИ через extension method:
await context.UpdateChatStatisticsAsync(chatRoomId);
```

---

## 🚀 Итоговая схема архитектуры

```
┌─────────────────────────────────────────────────────────────────┐
│                         UCHAT DATABASE                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  SQLite (Uchat.Database)              MongoDB (Messages)       │
│  ═══════════════════════              ═══════════════════      │
│                                                                 │
│  ┌──────────────┐                     ┌──────────────┐         │
│  │   Users      │                     │ MongoMessage │         │
│  ├──────────────┤                     ├──────────────┤         │
│  │ Id (PK)      │                     │ _id          │         │
│  │ Username     │◄────┐               │ ChatId ──────┼─┐       │
│  │ Email        │     │               │ Sender       │ │       │
│  └──────────────┘     │               │ Content      │ │       │
│         ▲             │               │ SentAt       │ │       │
│         │             │               └──────────────┘ │       │
│         │             │                                │       │
│  ┌──────────────┐     │                                │       │
│  │  ChatRooms   │     │                                │       │
│  ├──────────────┤     │                                │       │
│  │ Id (PK) ─────┼─────┼────────────────────────────────┘       │
│  │ Type (enum)  │     │  ┌─────────────────┐                  │
│  │ Name         │     │  │ DirectMessage=0 │                  │
│  │ ParentId ────┼─┐   │  │ Private=1       │                  │
│  │ CreatorId ───┼─┘   │  │ Public=2        │                  │
│  │ Settings...  │     │  │ Topic=3         │                  │
│  │ Stats...     │     │  │ Channel=4       │                  │
│  └──────────────┘     │  └─────────────────┘                  │
│         ▲             │                                        │
│         │             │                                        │
│  ┌──────────────┐     │                                        │
│  │ChatRoomMember│     │                                        │
│  ├──────────────┤     │                                        │
│  │ ChatRoomId ──┼─────┘                                        │
│  │ UserId ──────┼───────────────────────────────────────┐      │
│  │ Role (enum)  │  ┌─────────────┐                      │      │
│  │ JoinedAt     │  │ Member=0    │                      │      │
│  └──────────────┘  │ Admin=1     │                      │      │
│                    │ Owner=2     │                      │      │
│                    └─────────────┘                      │      │
│                                                          │      │
└──────────────────────────────────────────────────────────┼──────┘
                                                           │
                        ┌──────────────────────────────────┘
                        ▼
              Extensions/ChatRoomExtensions.cs
              ════════════════════════════════
              • GetEffective...()  ← Наследование настроек
              • CanUserSend...()   ← Проверка прав
              • GetTopicMembers()  ← Участники топиков
```

---

## ✅ ВЫВОДЫ

### Ты сделал ПРАВИЛЬНЫЙ выбор!

1. ✅ **Один класс ChatRoom** для всех типов чатов
2. ✅ **Nullable настройки** - используются по необходимости
3. ✅ **Self-referencing** для топиков
4. ✅ **Наследование** участников и настроек
5. ✅ **Extension methods** для чистого кода
6. ✅ **Статистика** для аналитики
7. ✅ **Гибкость** для будущих изменений

### Для личных чатов (DirectMessage):
- Настройки **игнорируются** (через GetEffective методы)
- Всегда **ровно 2 участника**
- Максимальная **простота**

### Для групп/каналов:
- Все настройки **работают**
- Гибкий **контроль доступа**
- **Slow Mode** для антиспама

### Для топиков:
- **Наследуют** участников от группы
- **Наследуют** или **переопределяют** настройки
- **ParentChatRoomId** для иерархии

---

**🎉 Твоя архитектура готова к production!**
