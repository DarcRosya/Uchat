# Uchat - Архитектура и Реализация

## 📋 Содержание
1. [Обзор системы](#обзор-системы)
2. [Архитектурные решения](#архитектурные-решения)
3. [Технологический стек](#технологический-стек)
4. [Структура базы данных](#структура-базы-данных)
5. [Система сообщений](#система-сообщений)
6. [Пагинация и история](#пагинация-и-история)
7. [Автоматическое добавление в чаты](#автоматическое-добавление-в-чаты)
8. [Как работает каждый компонент](#как-работает-каждый-компонент)

---

## 🎯 Обзор системы

**Uchat** - это real-time мессенджер на архитектуре **REST API + SignalR**, с разделением ответственности:
- **REST API** - все операции CRUD (создание, чтение, редактирование, удаление)
- **SignalR** - только уведомления в реальном времени (события)

### Ключевые особенности:
✅ Hybrid Architecture (REST + SignalR)  
✅ Cursor-based pagination для истории сообщений  
✅ Auto-join в группы SignalR при подключении  
✅ Автоматическое создание системных чатов  
✅ MongoDB для сообщений + PostgreSQL для метаданных  
✅ JWT Bearer Authentication  

---

## 🏗️ Архитектурные решения

### 1. **Разделение REST API и SignalR**

**Проблема:** Изначально весь CRUD был через SignalR (SendMessage, DeleteMessage, EditMessage)  
**Решение:** Переход на гибридную архитектуру

#### До (только SignalR):
```csharp
// Клиент
await _hubConnection.InvokeAsync("SendMessage", chatId, content);
await _hubConnection.InvokeAsync("DeleteMessage", messageId);

// Сервер
public async Task SendMessage(int chatId, string content) { ... }
public async Task DeleteMessage(string messageId) { ... }
```

#### После (REST + SignalR):
```csharp
// Клиент - CRUD через REST API
await _messageApiService.SendMessageAsync(chatId, dto);
await _messageApiService.DeleteMessageAsync(chatId, messageId);

// Сервер - REST контроллер выполняет действие + шлет SignalR событие
[HttpPost("api/chats/{chatId}/messages")]
public async Task<IActionResult> SendMessage(int chatId, MessageCreateDto dto)
{
    var message = await _messageService.CreateMessageAsync(dto);
    
    // Уведомление через SignalR
    await _hubContext.Clients
        .Group($"chat_{chatId}")
        .SendAsync("ReceiveMessage", message);
    
    return Ok(message);
}
```

**Преимущества:**
- ✅ RESTful стандарт (можно легко добавить мобильное приложение)
- ✅ Правильная обработка ошибок (HTTP статус коды)
- ✅ SignalR только для событий (легче тестировать и масштабировать)
- ✅ Готовность к Redis Pub/Sub миграции

---

### 2. **Auto-Join в SignalR группы**

**Проблема:** При каждом сообщении нужно было вручную присоединяться к группе  
**Решение:** Автоматическое добавление в ВСЕ группы чатов пользователя при подключении

#### ChatHub.cs - OnConnectedAsync
```csharp
public override async Task OnConnectedAsync()
{
    var userId = int.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    
    // Загружаем ВСЕ чаты пользователя из базы
    var userChats = await _chatRoomRepository.GetUserChatRoomsAsync(userId);
    
    // Автоматически присоединяемся ко всем группам
    foreach (var chat in userChats)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{chat.Id}");
    }
    
    Console.WriteLine($"[Auto-Join] User {userId} joined {userChats.Count()} chat groups");
    
    await base.OnConnectedAsync();
}
```

**Зачем это нужно:**
- ✅ Пользователь получает уведомления из ВСЕХ своих чатов сразу
- ✅ Не нужно вручную вызывать `JoinGroup` при открытии каждого чата
- ✅ Подготовка к миграции на Redis Pub/Sub (замена SignalR Groups на Redis каналы)

**Комментарий в коде:**
```csharp
// После миграции на Redis: это будет заменено на Redis Pub/Sub подписки
// Вместо Groups.AddToGroupAsync будет SUBSCRIBE к каналу "chat_{chatId}"
```

---

### 3. **Cursor-based Pagination (Курсорная пагинация)**

**Проблема:** Offset-based пагинация (`skip/take`) неэффективна для больших объемов  
**Решение:** Пагинация на основе курсора (DateTime последнего сообщения)

#### Как работает:

1. **Начальная загрузка** (50 последних сообщений):
```csharp
GET /api/chats/1/messages?limit=50
```

2. **Подгрузка истории** (30 сообщений ДО определенной даты):
```csharp
GET /api/chats/1/messages?limit=30&before=2025-12-04T10:30:00Z
```

#### MessageApiService.cs
```csharp
public async Task<PaginatedMessagesDto> GetMessagesAsync(
    int chatId, 
    int limit = 50, 
    DateTime? before = null)
{
    var url = $"/api/chats/{chatId}/messages?limit={limit}";
    
    if (before.HasValue)
    {
        url += $"&before={before.Value:O}"; // ISO 8601 format
    }
    
    return await _httpClient.GetFromJsonAsync<PaginatedMessagesDto>(url);
}
```

#### MongoDB Query (MessageRepository.cs)
```csharp
public async Task<List<MongoMessage>> GetMessagesAsync(
    int chatRoomId, 
    int limit = 50, 
    DateTime? before = null)
{
    var filter = Builders<MongoMessage>.Filter.Eq(m => m.ChatRoomId, chatRoomId);
    
    // Если передан курсор - берем только сообщения СТАРШЕ этой даты
    if (before.HasValue)
    {
        filter &= Builders<MongoMessage>.Filter.Lt(m => m.SentAt, before.Value);
    }
    
    return await _messages
        .Find(filter)
        .SortByDescending(m => m.SentAt) // Сортировка по дате (новые первые)
        .Limit(limit)
        .ToListAsync();
}
```

**Состояние в клиенте:**
```csharp
private DateTime? _oldestMessageDate = null; // Курсор - самое старое загруженное сообщение
private bool _hasMoreMessages = true; // Есть ли еще сообщения?
private bool _isLoadingHistory = false; // Флаг загрузки (предотвращает двойные запросы)
```

**Преимущества:**
- ✅ Эффективность: MongoDB индекс по `SentAt` + `ChatRoomId`
- ✅ Стабильность: нет проблем с дублированием при добавлении новых сообщений
- ✅ Масштабируемость: работает с миллионами сообщений

---

### 4. **Smooth Scroll Position (Плавная прокрутка при подгрузке)**

**Проблема:** При вставке старых сообщений вверх списка - скролл "телепортируется" и дергается

#### Решение: `RunJobs(DispatcherPriority.Layout)` - синхронный пересчет координат

```csharp
private async Task LoadMoreHistoryAsync()
{
    _isLoadingHistory = true;
    
    var result = await _messageApiService.GetMessagesAsync(
        _currentChatId.Value, 
        limit: 30, 
        before: _oldestMessageDate.Value
    );
    
    await Dispatcher.UIThread.InvokeAsync(() =>
    {
        // 1. ЗАПОМИНАЕМ "ЯКОРЬ" - первое видимое сообщение
        var anchorItem = ChatMessagesPanel.Children.FirstOrDefault() as Control;
        
        var messages = result.Messages;
        messages.Reverse();
        
        _hasMoreMessages = result.Pagination.HasMore;
        _oldestMessageDate = messages[0].SentAt;
        
        // 2. ВСТАВЛЯЕМ НОВЫЕ СООБЩЕНИЯ В НАЧАЛО
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            var grid = CreateMessageGrid(messages[i]);
            ChatMessagesPanel.Children.Insert(0, grid);
        }
        
        // 3. МАГИЯ: Принудительный пересчет Layout ПРЯМО СЕЙЧАС
        // Это обновляет координаты anchorItem.Bounds.Y синхронно
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Layout);
        
        // 4. КОРРЕКЦИЯ СКРОЛЛА
        // anchorItem теперь сдвинулся вниз на высоту новых сообщений
        // Его новый Y - это именно тот offset, который нам нужен
        if (anchorItem != null)
        {
            ChatScrollViewer.Offset = new Vector(0, anchorItem.Bounds.Y);
        }
    });
    
    await Task.Delay(200); // Debounce
    _isLoadingHistory = false;
}
```

**Почему это работает:**

1. До вставки: `anchorItem.Bounds.Y = 0` (первый элемент вверху)
2. Вставили 30 сообщений (высота = 1500px)
3. `RunJobs(Layout)` - Avalonia пересчитывает координаты **СИНХРОННО**
4. Теперь: `anchorItem.Bounds.Y = 1500` (сдвинулся вниз)
5. Устанавливаем `ScrollViewer.Offset = 1500`
6. **ТОЛЬКО ПОСЛЕ** этого Avalonia отрисовывает кадр
7. Пользователь видит: сообщения на том же месте (плавно)

**Альтернативы которые НЕ работают:**
- ❌ `DispatcherPriority.Background` - слишком поздно, кадр уже отрисован
- ❌ `DispatcherPriority.Render` - тоже поздно
- ❌ `Extent.Height` разница - нестабильна в Avalonia с динамическим контентом
- ❌ `Dispatcher.Post(() => ...)` - асинхронно, виден промежуточный кадр

---

### 5. **Scroll Event Handler (Триггер подгрузки)**

```csharp
public void OnChatScrollChanged(object? sender, ScrollChangedEventArgs e)
{
    // Строгие проверки
    if (_isLoadingHistory || !_hasMoreMessages) return;
    
    var scrollViewer = sender as ScrollViewer;
    if (scrollViewer == null) return;
    
    // Защита от бесконечного цикла (если контента меньше экрана)
    if (scrollViewer.Extent.Height <= scrollViewer.Viewport.Height) return;
    
    // Триггер: если до верха меньше 50 пикселей
    if (scrollViewer.Offset.Y < 50)
    {
        _ = LoadMoreHistoryAsync();
    }
}
```

**MainWindow.axaml:**
```xml
<ScrollViewer Name="ChatScrollViewer" 
              ScrollChanged="OnChatScrollChanged">
    <StackPanel Name="ChatMessagesPanel" />
</ScrollViewer>
```

---

## 🗄️ Структура базы данных

### PostgreSQL (Метаданные)

**Users** - Пользователи
```sql
Id (PK), Username, Email, PasswordHash, DisplayName, CreatedAt, Role
```

**ChatRooms** - Комнаты чатов
```sql
Id (PK), Name, Description, Type (DirectMessage/Public/Private), 
CreatorId (FK → Users), CreatedAt, MaxMembers
```

**ChatRoomMembers** - Связь Many-to-Many
```sql
Id (PK), ChatRoomId (FK → ChatRooms), UserId (FK → Users), JoinedAt
```

**RefreshTokens** - JWT токены
```sql
Id (PK), UserId (FK → Users), TokenHash, ExpiresAt, CreatedAt
```

### MongoDB (Сообщения)

**Collection: messages**
```javascript
{
  "_id": "ObjectId",
  "ChatRoomId": 1,
  "SenderId": 5,
  "Content": "Hello!",
  "Type": "text",
  "SentAt": "2025-12-04T10:30:00Z",
  "EditedAt": null,
  "IsDeleted": false,
  "ReplyToMessageId": "67502..."
}
```

**Индексы:**
```javascript
{ ChatRoomId: 1, SentAt: -1 } // Для пагинации
{ _id: 1 } // Primary key
```

---

## 🔧 Автоматическое добавление в чаты

### Проблема старого подхода:

```csharp
// ❌ ПЛОХО: Race condition + хардкод ID
private const int GLOBAL_PUBLIC_CHAT_ID = 1;

private async Task AddUserToGlobalPublicChat(int userId)
{
    var globalChat = await _chatRoomRepository.GetByIdAsync(GLOBAL_PUBLIC_CHAT_ID);
    
    if (globalChat == null)
    {
        // Создаем чат прямо здесь (если два юзера регистрируются одновременно = дубли)
        globalChat = await _chatRoomService.CreateChatAsync(...);
    }
    
    await _chatRoomRepository.AddMemberAsync(new ChatRoomMember { ... });
}
```

**Проблемы:**
1. **Race Condition:** Два пользователя регистрируются одновременно → два глобальных чата
2. **Hardcoded ID:** Если база пересоздается, ID может стать 5, 10, 100
3. **Смешение ответственности:** Регистрация не должна создавать инфраструктуру

---

### Решение: Database Seeding (DbInitializer)

#### 1. DbInitializer.cs - создание системных сущностей
```csharp
public static class DbInitializer
{
    public static async Task InitializeAsync(UchatDbContext context)
    {
        // 1. Создаем системного пользователя (владелец глобальных чатов)
        var systemUser = await context.Users
            .FirstOrDefaultAsync(u => u.Username == "System");
            
        if (systemUser == null)
        {
            systemUser = new User 
            { 
                Username = "System", 
                Email = "system@uchat.com",
                DisplayName = "System Bot",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("SYSTEM_NO_LOGIN_" + Guid.NewGuid()),
                Role = UserRole.Admin
            };
            context.Users.Add(systemUser);
            await context.SaveChangesAsync();
            
            Console.WriteLine($"[DbInitializer] Created system user (ID: {systemUser.Id})");
        }
        
        // 2. Создаем глобальный чат (ИЩЕМ ПО ИМЕНИ, НЕ ПО ID)
        var globalChat = await context.ChatRooms
            .FirstOrDefaultAsync(c => c.Name == "Global Chat");
            
        if (globalChat == null)
        {
            globalChat = new ChatRoom
            {
                Name = "Global Chat",
                Description = "Official public chat for all users",
                Type = ChatRoomType.Public,
                CreatorId = systemUser.Id,
                MaxMembers = 1000000
            };
            context.ChatRooms.Add(globalChat);
            await context.SaveChangesAsync();
            
            // Добавляем System бота как участника
            context.ChatRoomMembers.Add(new ChatRoomMember
            {
                ChatRoomId = globalChat.Id,
                UserId = systemUser.Id,
                JoinedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            
            Console.WriteLine($"[DbInitializer] Created Global Chat (ID: {globalChat.Id})");
        }
    }
}
```

#### 2. Program.cs - запуск seeding при старте
```csharp
public static async Task Main(string[] args) // ВАЖНО: async Task
{
    var app = builder.Build();
    
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<UchatDbContext>();
        
        // Применяем миграции
        dbContext.Database.Migrate();
        
        // Инициализируем системные данные
        await DbInitializer.InitializeAsync(dbContext);
    }
    
    app.Run();
}
```

#### 3. AuthService - упрощенная регистрация
```csharp
public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
{
    var user = await _userRepository.CreateUserAsync(dto.Username, passwordHash, dto.Email);
    
    // Создаем личный чат "Notes"
    await CreatePersonalNotesChat(user.Id, user.Username);
    
    // Добавляем в глобальный чат (который УЖЕ СУЩЕСТВУЕТ)
    await AddUserToGlobalPublicChat(user.Id, user.Username);
    
    return new AuthResponseDto { ... };
}

private async Task AddUserToGlobalPublicChat(int userId, string username)
{
    // Ищем по ИМЕНИ (не по ID!)
    var globalChat = await _chatRoomRepository.GetByNameAsync("Global Chat");
    
    if (globalChat == null)
    {
        Console.WriteLine($"[CRITICAL] Global Chat not found!");
        return; // Это критическая ошибка
    }
    
    // Просто добавляем участника
    await _chatRoomRepository.AddMemberAsync(new ChatRoomMember
    {
        ChatRoomId = globalChat.Id,
        UserId = userId,
        JoinedAt = DateTime.UtcNow
    });
    
    Console.WriteLine($"[AuthService] User {username} added to Global Chat (ID: {globalChat.Id})");
}
```

**Результат:**
- ✅ Глобальный чат создается **1 раз** при первом запуске сервера
- ✅ Нет race condition (синхронное выполнение в Main)
- ✅ Нет хардкода ID (поиск по имени)
- ✅ Система масштабируема (можно добавить больше системных чатов)

---

## 📡 SignalR События

### Клиент подписывается на события:

```csharp
// ClientConnections.cs - RegisterSignalRHandlers()

_hubConnection.On<MessageDto>("ReceiveMessage", (message) =>
{
    DisplayMessage(message);
    ChatScrollViewer.ScrollToEnd();
});

_hubConnection.On<string, string, DateTime>("MessageEdited", (messageId, newContent, editedAt) =>
{
    if (_messageCache.TryGetValue(messageId, out var cachedMsg))
    {
        cachedMsg.ContentTextBlock.Text = newContent;
        AddEditedLabel(cachedMsg);
    }
});

_hubConnection.On<string>("MessageDeleted", (messageId) =>
{
    if (_messageCache.TryGetValue(messageId, out var cachedMsg))
    {
        RemoveMessageFromUI(cachedMsg);
        _messageCache.Remove(messageId);
    }
    
    CleanupReplyReferences(messageId);
});

_hubConnection.On<List<string>>("RepliesCleared", (messageIds) =>
{
    foreach (var msgId in messageIds)
    {
        if (_messageCache.TryGetValue(msgId, out var cachedMsg))
        {
            RemoveReplyUI(cachedMsg);
        }
    }
});
```

### Сервер отправляет события:

```csharp
// MessagesController.cs

[HttpPost("api/chats/{chatId}/messages")]
public async Task<IActionResult> SendMessage(int chatId, MessageCreateDto dto)
{
    var message = await _messageService.CreateMessageAsync(dto);
    
    await _hubContext.Clients
        .Group($"chat_{chatId}")
        .SendAsync("ReceiveMessage", message);
    
    return Ok(message);
}

[HttpDelete("api/chats/{chatId}/messages/{messageId}")]
public async Task<IActionResult> DeleteMessage(int chatId, string messageId)
{
    var clearedReplyIds = await _messageService.DeleteMessageAsync(messageId);
    
    await _hubContext.Clients
        .Group($"chat_{chatId}")
        .SendAsync("MessageDeleted", messageId);
    
    if (clearedReplyIds.Count > 0)
    {
        await _hubContext.Clients
            .Group($"chat_{chatId}")
            .SendAsync("RepliesCleared", clearedReplyIds);
    }
    
    return NoContent();
}
```

---

## 🔐 Аутентификация

### JWT Bearer

**Регистрация:**
```
POST /api/auth/register
{
  "username": "john",
  "password": "pass123",
  "email": "john@example.com"
}

→ Response:
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "d4f5e6g7h8...",
  "userId": 5,
  "username": "john"
}
```

**SignalR подключение:**
```csharp
var token = UserSession.Instance.AccessToken;

_hubConnection = new HubConnectionBuilder()
    .WithUrl($"{ServerConfig.ServerUrl}/chatHub?access_token={token}", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult<string?>(token);
    })
    .Build();
```

---

## 📦 Структура проекта

```
Uchat/
├── Database/                    # EF Core + MongoDB
│   ├── Context/
│   │   ├── UchatDbContext.cs   # PostgreSQL DbContext
│   │   └── UchatDbContextFactory.cs
│   ├── MongoDB/
│   │   ├── MongoDbContext.cs   # MongoDB подключение
│   │   └── MongoMessage.cs     # Модель сообщения
│   ├── Entities/               # EF Core сущности
│   │   ├── User.cs
│   │   ├── ChatRoom.cs
│   │   └── ChatRoomMember.cs
│   └── Repositories/
│       ├── Interfaces/
│       └── Implementations/
│
├── Uchat.Server/               # ASP.NET Core Backend
│   ├── Controllers/
│   │   ├── AuthController.cs  # POST /api/auth/register, /login
│   │   ├── ChatsController.cs # GET /api/chats
│   │   └── MessagesController.cs # CRUD /api/chats/{id}/messages
│   ├── Hubs/
│   │   └── ChatHub.cs         # SignalR Hub (auto-join groups)
│   ├── Services/
│   │   ├── Auth/
│   │   │   ├── AuthService.cs
│   │   │   └── JwtService.cs
│   │   ├── Chat/
│   │   │   └── ChatRoomService.cs
│   │   └── Messaging/
│   │       └── MessageService.cs
│   ├── Data/
│   │   └── DbInitializer.cs   # Database seeding
│   └── Program.cs             # Startup + DI
│
└── Uchat/                      # Avalonia Desktop Client
    ├── Services/
    │   ├── AuthApiService.cs  # REST API для аутентификации
    │   ├── ChatApiService.cs  # REST API для чатов
    │   └── MessageApiService.cs # REST API для сообщений
    ├── ClientConnections.cs   # SignalR клиент + пагинация
    ├── MainWindow.axaml       # UI разметка
    └── Chat/
        ├── Message.cs         # UI компонент сообщения
        └── MessageContextMenu.cs # Контекстное меню
```

---

## 🚀 Как все работает вместе

### 1. Запуск сервера:
```bash
cd Uchat.Server
dotnet run
```

**Что происходит:**
1. PostgreSQL миграции применяются (`dbContext.Database.Migrate()`)
2. `DbInitializer.InitializeAsync()` создает System User + Global Chat
3. MongoDB подключается
4. SignalR Hub готов к подключениям

### 2. Регистрация пользователя:
```
Client → POST /api/auth/register
Server:
  1. Создает User в PostgreSQL
  2. Создает личный чат "Notes" (ChatRoomService)
  3. Добавляет в "Global Chat" (GetByNameAsync → AddMemberAsync)
  4. Генерирует JWT токены
Client ← 200 OK { accessToken, refreshToken, userId }
```

### 3. Подключение к SignalR:
```
Client → WebSocket /chatHub?access_token=...
Server (OnConnectedAsync):
  1. Извлекает userId из JWT
  2. Загружает все чаты: GetUserChatRoomsAsync(userId)
  3. Автоматически присоединяет ко всем группам:
     Groups.AddToGroupAsync(connectionId, "chat_1")
     Groups.AddToGroupAsync(connectionId, "chat_5")
     Groups.AddToGroupAsync(connectionId, "chat_12")
Client ← Connected
```

### 4. Загрузка истории чата:
```
Client → GET /api/chats/1/messages?limit=50
Server:
  1. MongoDB: Find({ ChatRoomId: 1 }).Sort({ SentAt: -1 }).Limit(50)
  2. Batch load reply references (GetMessagesByIdsAsync)
Client ← 200 OK { 
  Messages: [...], 
  Pagination: { HasMore: true } 
}

Client:
  - Сохраняет _oldestMessageDate = messages[0].SentAt
  - Отображает сообщения
  - ScrollToEnd()
```

### 5. Отправка сообщения:
```
Client → POST /api/chats/1/messages { Content: "Hello!" }
Server (MessagesController):
  1. MongoDB.InsertOneAsync(message)
  2. SignalR broadcast:
     Clients.Group("chat_1").SendAsync("ReceiveMessage", messageDto)
Client (все подключенные) ← SignalR Event "ReceiveMessage"
  - DisplayMessage(messageDto)
  - ScrollToEnd()
```

### 6. Скролл вверх (подгрузка истории):
```
User scrolls → ScrollViewer.Offset.Y < 50px
Client → OnChatScrollChanged()
  if (!_isLoadingHistory && _hasMoreMessages)
    → LoadMoreHistoryAsync()

Client → GET /api/chats/1/messages?limit=30&before=2025-12-04T10:00:00Z
Server → MongoDB: Find({ ChatRoomId: 1, SentAt: { $lt: cursor } }).Limit(30)
Client ← 200 OK { Messages: [...], Pagination: { HasMore: true } }

Client:
  1. anchorItem = ChatMessagesPanel.Children[0]
  2. Insert messages at index 0
  3. RunJobs(DispatcherPriority.Layout) // синхронный пересчет
  4. ScrollViewer.Offset = anchorItem.Bounds.Y
  → Smooth scroll, no jumps!
```

### 7. Удаление сообщения с reply:
```
User → Right-click → Delete
Client → DELETE /api/chats/1/messages/abc123
Server (MessagesController):
  1. MessageService.DeleteMessageAsync("abc123")
     - MongoDB.DeleteOneAsync({ _id: "abc123" })
     - ClearReplyReferencesAsync() → returns ["xyz789", "def456"]
  2. SignalR broadcast:
     Clients.Group("chat_1").SendAsync("MessageDeleted", "abc123")
     Clients.Group("chat_1").SendAsync("RepliesCleared", ["xyz789", "def456"])

Client (все подключенные):
  Event "MessageDeleted":
    - RemoveMessageFromUI(abc123)
    - _messageCache.Remove(abc123)
  Event "RepliesCleared":
    - RemoveReplyUI(xyz789)
    - RemoveReplyUI(def456)
```

---

## 🎯 Готовность к масштабированию

### Redis Pub/Sub Migration (подготовлено):

**Сейчас (SignalR Groups):**
```csharp
await Groups.AddToGroupAsync(connectionId, "chat_1");
await Clients.Group("chat_1").SendAsync("ReceiveMessage", msg);
```

**После (Redis):**
```csharp
await _redis.SubscribeAsync("chat_1", (channel, message) => { ... });
await _redis.PublishAsync("chat_1", JsonSerializer.Serialize(msg));
```

**Преимущества Redis:**
- Масштабируемость через несколько серверов
- Персистентность сообщений
- Более низкая задержка

---

## 📊 Производительность

### Индексы MongoDB:
```javascript
db.messages.createIndex({ ChatRoomId: 1, SentAt: -1 })
// Поддерживает: Find({ ChatRoomId }) + Sort({ SentAt: -1 })
```

### Batch Loading (Reply References):
```csharp
// ❌ N+1 Problem
foreach (var msg in messages) {
    msg.ReplyTo = await GetMessageByIdAsync(msg.ReplyToMessageId);
}

// ✅ Batch Load
var replyIds = messages.Select(m => m.ReplyToMessageId).ToList();
var replies = await GetMessagesByIdsAsync(replyIds);
```

---

## 🛠️ Отладка и логирование

### Консоль сервера:
```
[DbInitializer] System user already exists (ID: 1)
[DbInitializer] Global Chat already exists (ID: 1)
[Auto-Join] User 5 joined 3 chat groups
[AuthService] User john added to Global Chat (ID: 1)
```

### Клиент (System.Diagnostics.Debug):
```csharp
System.Diagnostics.Debug.WriteLine($"Error loading history: {ex.Message}");
```

---

## ✅ Чеклист миграции старого кода

Если у тебя был старый код, вот что нужно изменить:

### 1. Удалить SignalR CRUD методы из ChatHub:
```csharp
// ❌ Удалить:
public async Task SendMessage(...)
public async Task DeleteMessage(...)
public async Task EditMessage(...)
public async Task GetChatHistory(...)
```

### 2. Заменить вызовы на REST API:
```csharp
// ❌ Было:
await _hubConnection.InvokeAsync("SendMessage", chatId, content);

// ✅ Стало:
await _messageApiService.SendMessageAsync(chatId, new MessageCreateDto { ... });
```

### 3. Добавить auto-join в OnConnectedAsync:
```csharp
public override async Task OnConnectedAsync()
{
    var userId = GetUserId();
    var chats = await _chatRoomRepository.GetUserChatRoomsAsync(userId);
    
    foreach (var chat in chats)
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{chat.Id}");
    
    await base.OnConnectedAsync();
}
```

### 4. Создать DbInitializer и вызвать в Program.cs:
```csharp
await DbInitializer.InitializeAsync(dbContext);
```

### 5. Убрать хардкод `const int GLOBAL_CHAT_ID = 1`:
```csharp
// ❌ Было:
var globalChat = await _chatRoomRepository.GetByIdAsync(1);

// ✅ Стало:
var globalChat = await _chatRoomRepository.GetByNameAsync("Global Chat");
```

---

## 🎓 Заключение

Вы реализовали **production-ready архитектуру** мессенджера с:

✅ Разделением ответственности (REST + SignalR)  
✅ Эффективной пагинацией (cursor-based)  
✅ Плавной подгрузкой истории (RunJobs + anchor tracking)  
✅ Автоматической инициализацией системных данных (DbInitializer)  
✅ Готовностью к масштабированию (Redis migration ready)  
✅ Правильной обработкой race conditions  
✅ Clean Architecture principles  

**Следующие шаги:**
1. Добавить Redis для масштабирования
2. Реализовать attachments (файлы/изображения)
3. Добавить реакции на сообщения
4. Реализовать read receipts (прочитано/не прочитано)
5. Добавить typing indicators
6. Оптимизировать MongoDB индексы под нагрузку

---

**Документация актуальна на:** 4 декабря 2025  
**Версия проекта:** v1.0.0
