# 📖 ChatRoom - Примеры использования

## 🎯 Концепция: ВСЕ чаты через один класс ChatRoom

**Твой вопрос был абсолютно правильный!** Мы сделали так, что **любой чат** (личный, группа, канал, топик) хранится через **один класс ChatRoom**.

### ✅ Преимущества единого класса:
1. **Универсальность** - одни и те же методы для всех типов чатов
2. **Гибкость** - легко добавлять новые типы (супергруппы, форумы)
3. **Простота** - не нужно дублировать код для `DirectChats`, `Groups`, `Channels`
4. **Расширяемость** - новые поля добавляются один раз

---

## 📊 Типы чатов

| Тип | Описание | Участники | Кто может писать | Особенности |
|-----|----------|-----------|------------------|-------------|
| **DirectMessage** | Личный чат 1-on-1 | Всегда 2 | Оба | Настройки игнорируются |
| **Private** | Приватная группа | Много | По настройкам | Только по приглашению |
| **Public** | Публичная группа | Много | По настройкам | Можно найти и присоединиться |
| **Channel** | Канал | Много | Только админы | AllowMembersToSendMessages = false |
| **Topic** | Топик в группе | Наследуются | По настройкам | ParentChatRoomId != null |

---

## 1️⃣ DirectMessage - Личный чат

### Создание личного чата

```csharp
// ✅ ПРАВИЛЬНО: Использовать extension method
var chat = await context.GetOrCreateDirectChatAsync(user1Id, user2Id);

// Если чат уже существует - вернётся существующий
// Если нет - создастся новый с двумя участниками
```

### Проверка существующего чата

```csharp
var existingChat = await context.ChatRooms
    .Include(cr => cr.Members)
    .Where(cr => cr.Type == ChatRoomType.DirectMessage)
    .Where(cr => cr.Members.Any(m => m.UserId == user1Id))
    .Where(cr => cr.Members.Any(m => m.UserId == user2Id))
    .FirstOrDefaultAsync();

if (existingChat == null) {
    Console.WriteLine("Чат не найден, нужно создать");
} else {
    Console.WriteLine($"Чат существует: ID = {existingChat.Id}");
}
```

### Валидация DirectMessage

```csharp
// Проверка при добавлении участника
if (chatRoom.Type == ChatRoomType.DirectMessage) {
    var currentMembersCount = await context.ChatRoomMembers
        .CountAsync(m => m.ChatRoomId == chatRoomId);
    
    if (currentMembersCount >= 2) {
        throw new InvalidOperationException("DirectMessage can only have 2 members");
    }
}
```

### Особенности DirectMessage:
- ✅ `AllowMembersToInvite` → игнорируется (всегда `false`)
- ✅ `AllowMembersToSendMessages` → игнорируется (всегда `true`)
- ✅ `AllowMembersToSendMedia` → игнорируется (всегда `true`)
- ✅ `SlowModeSeconds` → игнорируется (всегда `null`)
- ✅ `MaxMembers` → игнорируется (всегда 2)

---

## 2️⃣ Private/Public Group - Группы

### Создание приватной группы

```csharp
// Создать группу
var group = new ChatRoom {
    Type = ChatRoomType.Private,
    Name = "Dev Team",
    Description = "Обсуждение разработки проекта",
    CreatorId = userId,
    CreatedAt = DateTime.UtcNow,
    
    // Настройки
    AllowMembersToInvite = false,      // Только админы приглашают
    AllowMembersToSendMessages = true, // Все могут писать
    AllowMembersToSendMedia = true,    // Медиа разрешены
    SlowModeSeconds = null,            // Без ограничений
    MaxMembers = 200
};
context.ChatRooms.Add(group);
await context.SaveChangesAsync();

// Добавить создателя как Owner
context.ChatRoomMembers.Add(new ChatRoomMember {
    ChatRoomId = group.Id,
    UserId = userId,
    Role = ChatRoomRole.Owner,
    JoinedAt = DateTime.UtcNow
});
await context.SaveChangesAsync();
```

### Создание публичной группы

```csharp
var publicGroup = new ChatRoom {
    Type = ChatRoomType.Public,
    Name = "Ukrainian Developers",
    Description = "Community of Ukrainian developers",
    CreatorId = userId,
    
    // Настройки для публичной группы
    AllowMembersToInvite = true,       // Любой может приглашать
    AllowMembersToSendMessages = true,
    SlowModeSeconds = 5                // 1 сообщение в 5 секунд (антиспам)
};
```

### Добавление участника в группу

```csharp
// Проверить лимит участников
var currentCount = await context.ChatRoomMembers
    .CountAsync(m => m.ChatRoomId == groupId);

if (group.MaxMembers.HasValue && currentCount >= group.MaxMembers.Value) {
    throw new InvalidOperationException("Group is full");
}

// Добавить участника
var member = new ChatRoomMember {
    ChatRoomId = groupId,
    UserId = newUserId,
    Role = ChatRoomRole.Member,
    JoinedAt = DateTime.UtcNow,
    InvitedById = inviterUserId
};
context.ChatRoomMembers.Add(member);
await context.SaveChangesAsync();
```

### Проверка прав перед отправкой сообщения

```csharp
// Использовать extension method
if (!await context.CanUserSendMessageAsync(groupId, userId)) {
    return BadRequest("You cannot send messages in this group");
}

// Для медиа
if (hasAttachment && !await context.CanUserSendMediaAsync(groupId, userId)) {
    return BadRequest("Media is not allowed in this group");
}
```

---

## 3️⃣ Channel - Канал

### Создание канала

```csharp
var channel = new ChatRoom {
    Type = ChatRoomType.Channel,
    Name = "Tech News",
    Description = "Latest news from tech world",
    CreatorId = userId,
    
    // ⚠️ ВАЖНО: В каналах только админы пишут!
    AllowMembersToSendMessages = false,  // ← Ключевая настройка
    AllowMembersToSendMedia = false,     // Только админы
    MaxMembers = 100000                  // Каналы могут быть большими
};
context.ChatRooms.Add(channel);
await context.SaveChangesAsync();

// Создатель становится Owner
context.ChatRoomMembers.Add(new ChatRoomMember {
    ChatRoomId = channel.Id,
    UserId = userId,
    Role = ChatRoomRole.Owner
});
await context.SaveChangesAsync();
```

### Добавление подписчиков (не участников!)

```csharp
// В канале участники = подписчики (subscribers)
// Они только читают, не пишут
var subscriber = new ChatRoomMember {
    ChatRoomId = channelId,
    UserId = subscriberId,
    Role = ChatRoomRole.Member,  // Обычный member = подписчик
    JoinedAt = DateTime.UtcNow
};
```

### Проверка прав для канала

```csharp
var member = await context.ChatRoomMembers
    .FirstAsync(m => m.ChatRoomId == channelId && m.UserId == userId);

// В канале писать могут только админы/владельцы
if (member.Role != ChatRoomRole.Admin && member.Role != ChatRoomRole.Owner) {
    throw new ForbiddenException("Only admins can post in channels");
}
```

---

## 4️⃣ Topic - Топик внутри группы

### Создание топика

```csharp
// Сначала проверить, что родитель может иметь топики
var parentGroup = await context.ChatRooms.FindAsync(parentGroupId);

if (!parentGroup.CanHaveTopics()) {
    throw new InvalidOperationException("This chat type cannot have topics");
}

// Создать топик
var topic = new ChatRoom {
    Type = ChatRoomType.Topic,
    Name = "General Discussion",
    ParentChatRoomId = parentGroupId,  // ← Связь с группой
    CreatorId = userId,
    CreatedAt = DateTime.UtcNow,
    
    // Настройки можно задать или оставить null (наследуются от родителя)
    AllowMembersToSendMessages = null  // null = наследовать от родителя
};
context.ChatRooms.Add(topic);
await context.SaveChangesAsync();
```

### Получение участников топика

```csharp
// ✅ ПРАВИЛЬНО: Участники наследуются от родительской группы
var members = await context.GetTopicMembersAsync(topicId);

foreach (var user in members) {
    Console.WriteLine($"- {user.Username}");
}

// ❌ НЕПРАВИЛЬНО: Искать в ChatRoomMembers для топика
// У топика НЕТ своих записей в ChatRoomMembers!
// Участники берутся из ParentChatRoom.Members
```

### Получение эффективных настроек топика

```csharp
var topic = await context.ChatRooms
    .Include(cr => cr.ParentChatRoom)
    .FirstAsync(cr => cr.Id == topicId);

// Если у топика свои настройки - использовать их
// Если null - взять от родителя
bool canWrite = topic.GetEffectiveAllowMembersToSendMessages();
bool canInvite = topic.GetEffectiveAllowMembersToInvite();
int? slowMode = topic.GetEffectiveSlowModeSeconds();

Console.WriteLine($"Can write: {canWrite}");
Console.WriteLine($"Slow mode: {slowMode ?? 0} seconds");
```

### Пример: Закрытый топик внутри открытой группы

```csharp
// Группа: все могут писать
var group = new ChatRoom {
    Type = ChatRoomType.Private,
    Name = "Dev Team",
    AllowMembersToSendMessages = true  // ← Все могут
};

// Топик: только админы могут писать
var adminTopic = new ChatRoom {
    Type = ChatRoomType.Topic,
    Name = "Admin Announcements",
    ParentChatRoomId = group.Id,
    AllowMembersToSendMessages = false  // ← Переопределяем настройку родителя
};

// Проверка:
var member = await context.ChatRoomMembers.FirstAsync(...);
if (member.Role != ChatRoomRole.Admin) {
    bool canWrite = adminTopic.GetEffectiveAllowMembersToSendMessages();
    // canWrite = false (берётся настройка топика, не группы)
}
```

---

## 5️⃣ Работа со статистикой

### Обновление статистики при новом сообщении

```csharp
// После отправки сообщения в MongoDB
await mongoRepo.SendMessageAsync(new MongoMessage {
    ChatId = chatRoomId,
    Sender = new MessageSender { UserId = userId, Username = "john" },
    Content = "Hello!",
    SentAt = DateTime.UtcNow
});

// ✅ Обновить статистику в SQLite
await context.UpdateChatStatisticsAsync(chatRoomId);

// Теперь:
// - TotalMessagesCount увеличился на 1
// - LastActivityAt = DateTime.UtcNow
```

### Получение активных чатов

```csharp
// Сортировка по последней активности
var activeChats = await context.ChatRooms
    .Where(cr => cr.Members.Any(m => m.UserId == userId))  // Мои чаты
    .OrderByDescending(cr => cr.LastActivityAt)             // Сначала активные
    .Take(20)
    .ToListAsync();

foreach (var chat in activeChats) {
    var timeSinceActivity = DateTime.UtcNow - (chat.LastActivityAt ?? chat.CreatedAt);
    Console.WriteLine($"{chat.Name}: {chat.TotalMessagesCount} messages, active {timeSinceActivity.TotalHours:F1}h ago");
}
```

---

## 6️⃣ Проверка прав с учётом всех типов

### Универсальная проверка прав на отправку

```csharp
public async Task<SendMessageResult> SendMessageAsync(
    int chatRoomId, 
    int userId, 
    string content,
    List<Attachment> attachments)
{
    var chatRoom = await context.ChatRooms
        .Include(cr => cr.ParentChatRoom)
        .FirstAsync(cr => cr.Id == chatRoomId);

    // 1. Проверить, может ли писать сообщения
    if (!await context.CanUserSendMessageAsync(chatRoomId, userId)) {
        return new SendMessageResult { 
            Success = false, 
            Error = "You cannot send messages in this chat" 
        };
    }

    // 2. Проверить права на медиа
    if (attachments.Any() && !await context.CanUserSendMediaAsync(chatRoomId, userId)) {
        return new SendMessageResult { 
            Success = false, 
            Error = "You cannot send media in this chat" 
        };
    }

    // 3. Проверить Slow Mode
    var slowMode = chatRoom.GetEffectiveSlowModeSeconds();
    if (slowMode.HasValue) {
        var lastMessage = await mongoRepo.GetLastUserMessageAsync(chatRoomId, userId);
        if (lastMessage != null) {
            var timeSince = DateTime.UtcNow - lastMessage.SentAt;
            if (timeSince.TotalSeconds < slowMode.Value) {
                var waitTime = slowMode.Value - (int)timeSince.TotalSeconds;
                return new SendMessageResult { 
                    Success = false, 
                    Error = $"Slow mode: wait {waitTime} seconds" 
                };
            }
        }
    }

    // 4. Отправить сообщение
    var message = new MongoMessage {
        ChatId = chatRoomId,
        Sender = new MessageSender { UserId = userId, ... },
        Content = content,
        Attachments = attachments.Select(a => new MessageAttachment { ... }).ToList(),
        SentAt = DateTime.UtcNow
    };
    await mongoRepo.SendMessageAsync(message);

    // 5. Обновить статистику
    await context.UpdateChatStatisticsAsync(chatRoomId);

    return new SendMessageResult { Success = true, MessageId = message.Id };
}
```

---

## 7️⃣ Получение списка чатов пользователя

### Все чаты с информацией

```csharp
var userChats = await context.ChatRooms
    .Where(cr => cr.Members.Any(m => m.UserId == userId))
    .Select(cr => new ChatListItem {
        Id = cr.Id,
        Type = cr.Type,
        Name = cr.Type == ChatRoomType.DirectMessage 
            ? null  // Имя сгенерируется на клиенте
            : cr.Name,
        AvatarUrl = cr.AvatarUrl,
        TotalMessagesCount = cr.TotalMessagesCount,
        LastActivityAt = cr.LastActivityAt,
        
        // Для DirectMessage получить имя собеседника
        OtherUserName = cr.Type == ChatRoomType.DirectMessage
            ? cr.Members.First(m => m.UserId != userId).User.Username
            : null,
        
        // Для топиков получить имя родительской группы
        ParentGroupName = cr.Type == ChatRoomType.Topic && cr.ParentChatRoom != null
            ? cr.ParentChatRoom.Name
            : null
    })
    .OrderByDescending(c => c.LastActivityAt)
    .ToListAsync();
```

### Только личные чаты

```csharp
var directChats = await context.ChatRooms
    .Include(cr => cr.Members)
        .ThenInclude(m => m.User)
    .Where(cr => cr.Type == ChatRoomType.DirectMessage)
    .Where(cr => cr.Members.Any(m => m.UserId == userId))
    .ToListAsync();

foreach (var chat in directChats) {
    var otherUser = chat.Members.First(m => m.UserId != userId).User;
    Console.WriteLine($"Chat with {otherUser.Username} ({chat.TotalMessagesCount} messages)");
}
```

### Только группы и каналы

```csharp
var groups = await context.ChatRooms
    .Where(cr => cr.Type == ChatRoomType.Private || cr.Type == ChatRoomType.Public || cr.Type == ChatRoomType.Channel)
    .Where(cr => cr.Members.Any(m => m.UserId == userId))
    .Include(cr => cr.Topics)  // Загрузить топики
    .ToListAsync();

foreach (var group in groups) {
    Console.WriteLine($"{group.Name} ({group.Type}): {group.Topics.Count} topics");
    foreach (var topic in group.Topics) {
        Console.WriteLine($"  - {topic.Name}");
    }
}
```

---

## 🎓 ВЫВОДЫ

### ✅ Что мы реализовали:

1. **Единый класс ChatRoom** для всех типов чатов
2. **5 типов чатов**: DirectMessage, Private, Public, Channel, Topic
3. **Гибкие настройки** с nullable полями (используются defaults)
4. **Наследование настроек** для топиков от родительских групп
5. **Статистика** - TotalMessagesCount, LastActivityAt
6. **Extension methods** - готовые методы для проверки прав
7. **Валидация** - разные правила для разных типов

### 📝 Для личных чатов (DirectMessage):
- ✅ Настройки **игнорируются** (всегда defaults)
- ✅ Всегда **ровно 2 участника**
- ✅ Оба могут писать, отправлять медиа
- ✅ Нет админов, ролей, ограничений

### 📝 Для групп/каналов:
- ✅ Все настройки **работают**
- ✅ Есть **роли** (Owner, Admin, Member)
- ✅ Гибкие **разрешения**
- ✅ **Slow Mode** для антиспама

### 📝 Для топиков:
- ✅ **Наследуют участников** от родительской группы
- ✅ **Наследуют настройки** (если не заданы свои)
- ✅ Можно **переопределить** настройки
- ✅ **ParentChatRoomId** для связи

---

## 🚀 Следующие шаги

1. Создать миграцию:
   ```bash
   cd Uchat.Database
   dotnet ef migrations add AddChatRoomEnhancements
   dotnet ef database update
   ```

2. Обновить MongoDB репозиторий для обновления статистики

3. Добавить валидацию на уровне API (контроллеры)

4. Реализовать Slow Mode проверку

5. Добавить UI для управления настройками групп
