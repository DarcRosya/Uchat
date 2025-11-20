# Настройка базы данных для Uchat

## Архитектура

Uchat использует **гибридную архитектуру баз данных**:

### PostgreSQL (Supabase) - для структурированных данных
- ✅ **Users** - пользователи
- ✅ **ChatRooms** - чаты и группы
- ✅ **ChatRoomMembers** - участники чатов
- ✅ **Contacts** - контакты пользователей
- ✅ **Friendships** - запросы в друзья

### MongoDB (Atlas) - для сообщений
- ✅ **Messages** - сообщения в чатах (высокая нагрузка)

---

## 1️⃣ Настройка Supabase PostgreSQL

### Шаг 1: Получите строку подключения

1. Откройте [Supabase Dashboard](https://app.supabase.com/)
2. Выберите проект → **Settings** → **Database**
3. Скопируйте **Connection String** (формат: URI)

Строка будет выглядеть так:
```
postgresql://postgres:[YOUR-PASSWORD]@db.xxx.supabase.co:5432/postgres
```

### Шаг 2: Добавьте в appsettings.json

Откройте `.config/appsettings.json` и замените:

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=db.xxx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR-PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

**Важно**: Замените `YOUR-PASSWORD` на ваш пароль из Supabase!

### Шаг 3: Примените миграции

**Для локальной разработки (SQLite):**
```bash
cd Uchat.Database
dotnet ef database update
```

Это создаст файл `uchat.db` в папке проекта.

**Для Supabase PostgreSQL:**
```bash
cd Uchat.Database
dotnet ef database update --connection "Host=db.xxx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR-PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
```

**Важно**: Замените `YOUR-PASSWORD` на ваш пароль из Supabase!

---

## 2️⃣ Настройка MongoDB Atlas

### Уже готово! ✅

Строка подключения уже добавлена в `.config/appsettings.json`:

```json
{
  "MongoDb": {
    "ConnectionString": "mongodb+srv://darcrosya:u5IpEy5s8FvWw9ZS@uchat-dev.b4ajiop.mongodb.net/?appName=uchat-dev",
    "DatabaseName": "uchat-dev",
    "MessagesCollectionName": "messages"
  }
}
```

MongoDB создаст коллекцию `messages` автоматически при первой вставке.

---

## 3️⃣ Использование в коде

### Dependency Injection (Program.cs)

```csharp
using Uchat.Database.Context;
using Uchat.Database.MongoDB;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// PostgreSQL (Supabase) - для Users, ChatRooms и т.д.
builder.Services.AddDbContext<UchatDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

// MongoDB (Atlas) - для Messages
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDb"));
builder.Services.AddSingleton<MongoDbContext>();

var app = builder.Build();
```

### Пример использования

```csharp
public class ChatService
{
    private readonly UchatDbContext _pgContext;      // PostgreSQL
    private readonly MongoDbContext _mongoContext;   // MongoDB
    
    public ChatService(UchatDbContext pgContext, MongoDbContext mongoContext)
    {
        _pgContext = pgContext;
        _mongoContext = mongoContext;
    }
    
    public async Task SendMessageAsync(int chatId, int userId, string content)
    {
        // 1. Получить данные пользователя из PostgreSQL
        var user = await _pgContext.Users.FindAsync(userId);
        
        // 2. Сохранить сообщение в MongoDB
        var message = new MongoMessage
        {
            ChatId = chatId,
            Sender = new MessageSender
            {
                UserId = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl
            },
            Content = content,
            Type = "text",
            SentAt = DateTime.UtcNow
        };
        
        await _mongoContext.Messages.InsertOneAsync(message);
        
        // 3. Обновить LastActivityAt в PostgreSQL
        var chatRoom = await _pgContext.ChatRooms.FindAsync(chatId);
        chatRoom.LastActivityAt = DateTime.UtcNow;
        await _pgContext.SaveChangesAsync();
    }
}
```

---

## Проверка подключения

```csharp
// PostgreSQL
using (var scope = app.Services.CreateScope())
{
    var pgContext = scope.ServiceProvider.GetRequiredService<UchatDbContext>();
    var canConnect = await pgContext.Database.CanConnectAsync();
    Console.WriteLine($"PostgreSQL: {(canConnect ? "✅ Connected" : "❌ Failed")}");
}

// MongoDB
var mongoContext = app.Services.GetRequiredService<MongoDbContext>();
var mongoConnected = await mongoContext.IsConnectedAsync();
Console.WriteLine($"MongoDB: {(mongoConnected ? "✅ Connected" : "❌ Failed")}");
```

---

## 📋 Чеклист настройки

- [ ] Создан проект в Supabase
- [ ] Скопирована connection string для PostgreSQL
- [ ] Обновлен `.config/appsettings.json`
- [ ] Применены миграции: `dotnet ef database update`
- [ ] Проверено подключение к PostgreSQL
- [ ] Проверено подключение к MongoDB Atlas
- [ ] Настроен Dependency Injection в `Program.cs`

---

## 🔒 Безопасность

⚠️ **Никогда не коммитьте `appsettings.json` с паролями в Git!**

Используйте **User Secrets** для локальной разработки:

```bash
cd Uchat.Database
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:PostgreSQL" "Host=..."
dotnet user-secrets set "MongoDb:ConnectionString" "mongodb+srv://..."
```

Для production используйте **переменные окружения**.
