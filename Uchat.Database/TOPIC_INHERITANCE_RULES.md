# 📋 Правила наследования для топиков

## 🎯 Ключевое правило

**Топики ВСЕГДА наследуют настройки от родительской группы и НЕ МОГУТ их переопределить.**

---

## ✅ Что наследуется от родителя

### 1. **Участники** (Members)
```csharp
// ❌ У топика НЕТ своих участников в ChatRoomMembers!
// ✅ Участники берутся из ParentChatRoom.Members

var members = await context.GetTopicMembersAsync(topicId);
// → Возвращает ParentChatRoom.Members
```

### 2. **Разрешения** (Permissions)
```csharp
// topic.AllowMembersToInvite - ИГНОРИРУЕТСЯ (всегда NULL)
// topic.AllowMembersToSendMessages - ИГНОРИРУЕТСЯ (всегда NULL)
// topic.AllowMembersToSendMedia - ИГНОРИРУЕТСЯ (всегда NULL)
// topic.SlowModeSeconds - ИГНОРИРУЕТСЯ (всегда NULL)

// ✅ Эффективные значения ВСЕГДА от родителя:
bool canInvite = topic.GetEffectiveAllowMembersToInvite();
// → ParentChatRoom.GetEffectiveAllowMembersToInvite()

bool canWrite = topic.GetEffectiveAllowMembersToSendMessages();
// → ParentChatRoom.GetEffectiveAllowMembersToSendMessages()
```

---

## 🔐 Логика Extension Methods

### До изменения (НЕПРАВИЛЬНО):
```csharp
// ❌ Топик мог переопределять настройки:
if (chatRoom.Type == ChatRoomType.Topic) {
    return chatRoom.AllowMembersToSendMessages  // ← Проверяли свои
        ?? chatRoom.ParentChatRoom?.AllowMembersToSendMessages
        ?? true;
}
```

### После изменения (ПРАВИЛЬНО):
```csharp
// ✅ Топик ВСЕГДА использует настройки родителя:
if (chatRoom.Type == ChatRoomType.Topic && chatRoom.ParentChatRoom != null) {
    return chatRoom.ParentChatRoom.GetEffectiveAllowMembersToSendMessages();
    // chatRoom.AllowMembersToSendMessages ИГНОРИРУЕТСЯ!
}
```

---

## 📊 Примеры

### Пример 1: Создание топика

```csharp
// ✅ ПРАВИЛЬНО: Оставить настройки NULL
var topic = new ChatRoom {
    Type = ChatRoomType.Topic,
    Name = "General Discussion",
    ParentChatRoomId = parentGroupId,
    CreatorId = userId,
    
    // ВАЖНО: Эти поля оставляем NULL (игнорируются)
    AllowMembersToInvite = null,
    AllowMembersToSendMessages = null,
    AllowMembersToSendMedia = null,
    SlowModeSeconds = null
};

// ❌ НЕПРАВИЛЬНО: Устанавливать значения (будут игнорироваться)
var topic = new ChatRoom {
    Type = ChatRoomType.Topic,
    // ...
    AllowMembersToSendMessages = false  // ← Бесполезно! Будет игнорироваться
};
```

### Пример 2: Проверка прав

```csharp
var topic = await context.ChatRooms
    .Include(cr => cr.ParentChatRoom)
    .FirstAsync(cr => cr.Id == topicId);

// ✅ ПРАВИЛЬНО: Использовать extension method
bool canWrite = topic.GetEffectiveAllowMembersToSendMessages();
// → Вернёт ParentChatRoom.GetEffectiveAllowMembersToSendMessages()

// ❌ НЕПРАВИЛЬНО: Читать напрямую
bool canWrite = topic.AllowMembersToSendMessages ?? true;
// → Всегда вернёт true (т.к. поле NULL), неправильно!
```

### Пример 3: Разные настройки группы и топика?

```csharp
// Группа: все могут писать
var group = new ChatRoom {
    Type = ChatRoomType.Private,
    Name = "Dev Team",
    AllowMembersToSendMessages = true  // ← Все могут
};

// Топик: хотим только для админов?
var topic = new ChatRoom {
    Type = ChatRoomType.Topic,
    Name = "Важные объявления",
    ParentChatRoomId = group.Id,
    AllowMembersToSendMessages = false  // ← Бесполезно! ИГНОРИРУЕТСЯ!
};

// Проверка:
bool canWrite = topic.GetEffectiveAllowMembersToSendMessages();
// → Вернёт true (от родителя), а НЕ false!

// ❌ НЕВОЗМОЖНО сделать топик с другими настройками!
// ✅ Если нужны разные права - создавай отдельную группу
```

---

## 🧬 Что МОЖНО менять в топике

### ✅ Разрешено менять:
- `Name` - имя топика
- `AvatarUrl` - иконка топика
- `TotalMessagesCount` - статистика (обновляется автоматически)
- `LastActivityAt` - последняя активность

### ❌ ИГНОРИРУЕТСЯ (всегда NULL):
- `Description` - топики не имеют описания (только имя)
- `AllowMembersToInvite`
- `AllowMembersToSendMessages`
- `AllowMembersToSendMedia`
- `SlowModeSeconds`
- `MaxMembers`

---

## 🎨 Визуализация наследования

```
┌─────────────────────────────────┐
│ ChatRoom #1 (Group)             │
│ Type: Private                   │
│ AllowMembersToSendMessages: true│ ← Источник настроек
│ SlowModeSeconds: 5              │
└─────────────────────────────────┘
              ▲
              │ Наследование (ТОЛЬКО чтение)
              │
┌─────────────────────────────────┐
│ ChatRoom #2 (Topic)             │
│ Type: Topic                     │
│ ParentChatRoomId: 1             │
│ AllowMembers*: NULL             │ ← Игнорируются
│ SlowModeSeconds: NULL           │
└─────────────────────────────────┘
              │
              │ GetEffective() методы
              ↓
    ParentChatRoom.AllowMembersToSendMessages → true
    ParentChatRoom.SlowModeSeconds → 5
```

---

## 🔍 Проверка кода

### Рекомендации при code review:

```csharp
// ❌ ПЛОХО: Прямое обращение к настройкам топика
if (topic.AllowMembersToSendMessages == true) {
    // ...
}

// ✅ ХОРОШО: Через extension method
if (topic.GetEffectiveAllowMembersToSendMessages()) {
    // ...
}

// ❌ ПЛОХО: Установка настроек для топика
topic.AllowMembersToSendMessages = false;

// ✅ ХОРОШО: Настройки только для родителя
if (topic.ParentChatRoom != null) {
    topic.ParentChatRoom.AllowMembersToSendMessages = false;
}

// ❌ ПЛОХО: Поиск участников топика
var members = await context.ChatRoomMembers
    .Where(m => m.ChatRoomId == topicId)
    .ToListAsync();
// → Вернёт пустой список!

// ✅ ХОРОШО: Через extension method
var members = await context.GetTopicMembersAsync(topicId);
// → Вернёт участников родительской группы
```

---

## 📋 Итоговая таблица

| Поле | DirectMessage | Group/Channel | Topic |
|------|---------------|---------------|-------|
| **AllowMembersToInvite** | NULL (игн.) | ✅ Работает | NULL (от родителя) |
| **AllowMembersToSendMessages** | NULL (игн.) | ✅ Работает | NULL (от родителя) |
| **AllowMembersToSendMedia** | NULL (игн.) | ✅ Работает | NULL (от родителя) |
| **SlowModeSeconds** | NULL (игн.) | ✅ Работает | NULL (от родителя) |
| **MaxMembers** | игн. (всегда 2) | ✅ Работает | NULL (игн.) |
| **Members** | 2 участника | ✅ Много | ❌ Наследуются |
| **Name** | NULL | ✅ Работает | ✅ Работает |
| **Description** | NULL | ✅ Работает | ❌ NULL (нет описания) |
| **AvatarUrl** | NULL | ✅ Работает | ✅ Работает |

---

## ✅ Выводы

1. **Топики - это просто организация обсуждений** внутри существующей группы
2. **Все настройки наследуются** от родительской группы
3. **Участники общие** для группы и всех её топиков
4. **Нельзя переопределить** права доступа в топике
5. **Если нужны разные права** - создай отдельную группу

**Это упрощает логику и делает систему предсказуемой!** 🎉
