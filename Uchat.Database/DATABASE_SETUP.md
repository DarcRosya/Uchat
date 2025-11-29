# Настройка базы данных для Uchat

## Архитектура

Uchat использует **гибридную архитектуру баз данных**:

### SQLite - для структурированных данных
- ✅ **Users** - пользователи
- ✅ **ChatRooms** - чаты и группы
- ✅ **ChatRoomMembers** - участники чатов
- ✅ **Contacts** - контакты пользователей
- ✅ **Friendships** - запросы в друзья

### LiteDB - для сообщений
- ✅ **Messages** - сообщения в чатах (высокая нагрузка)

---

## 1️⃣ Настройка SQLite

### Шаг 1: Настройка подключения

SQLite - это встраиваемая база данных, которая хранится в одном файле. Не требует установки сервера!

Откройте `.config/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "SQLite": "Data Source=uchat.db"
  }
}
```

**Примечания:**
- `uchat.db` - файл базы данных будет создан автоматически
- Можно указать путь: `"Data Source=Data/uchat.db"`
- Или абсолютный путь: `"Data Source=C:/Databases/uchat.db"`

### Шаг 2: Примените миграции

```bash
cd Uchat.Database
dotnet ef database update
```

Это создаст файл `uchat.db` с всеми таблицами!

---

## 2️⃣ Настройка LiteDB

### Автоматическая настройка ✅

LiteDB - это легковесная NoSQL база данных для .NET (аналог SQLite для документов).

Настройки уже добавлены в `.config/appsettings.json`:

```json
{
    "LiteDb": {
        "DatabasePath": "Data/messages.db",
        "MessagesCollectionName": "messages",
        "RetentionDays": 30,
        "CleanupIntervalMinutes": 60,
        "BackupDirectory": "Backups",
        "BackupRetention": 7,
        "BackupIntervalMinutes": 1440,
        "EnableSharding": false,
        "ShardFilePattern": "messages-{chatId}.db"
    }
}
```
`RetentionDays`, `CleanupIntervalMinutes`, `BackupDirectory`, `BackupRetention` и `BackupIntervalMinutes` используются соответствующими хост-сервисами; настраивайте их под нагрузку. `EnableSharding` и `ShardFilePattern` позволяют создавать отдельные файлы на чат (`messages-<chatId>.db`), если вы разделяете данные. Для этого заведите фабрику `LiteDbContext`, которая подставляет `ShardFilePattern.Replace("{chatId}", chatId.ToString())` при создании `LiteDatabase`.

`RetentionDays` и `CleanupIntervalMinutes` используются `MessageCleanupService`, поэтому обновите значения под вашу нагрузку (например, 7 дней для тестов, 60 минут между циклами).

**Файл `messages.db` создастся автоматически при первом запуске!**

**Преимущества LiteDB:**
- Один файл базы данных
- Не требует установки сервера
- Поддержка LINQ запросов
- ACID транзакции
- Размер БД до 2 ТБ

---

## 3️⃣ Использование в коде

### Dependency Injection (Program.cs)

```csharp
using Uchat.Database.Context;
using Uchat.Database.LiteDB;
using Uchat.Database.Services.Messaging;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// SQLite - для Users, ChatRooms и т.д.
builder.Services.AddDbContext<UchatDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SQLite")));

// LiteDB - для Messages
builder.Services.Configure<LiteDbSettings>(
    builder.Configuration.GetSection("LiteDb"));
builder.Services.AddSingleton<LiteDbContext>();
builder.Services.AddScoped<IMessagingCoordinator, MessagingCoordinator>();
builder.Services.AddHostedService<MessageCleanupService>();

// new chat & friendship services
builder.Services.AddScoped<ITransactionRunner, TransactionRunner>();
builder.Services.AddScoped<IChatRoomService, ChatRoomService>();
builder.Services.AddScoped<IFriendshipService, FriendshipService>();

var app = builder.Build();
```

### Сервисный слой и результаты

`ITransactionRunner` обеспечивает, что операции с несколькими репозиториями (`ChatRoomService`, `FriendshipService`) либо коммитятся полностью, либо откатываются. Сервисы (регистрации выше) используют DTO и `Result`/`ChatResult`, чтобы сразу возвращать понятные ошибки без лишних транзакций.

### Юнит-тесты сервисов

Проект `tests/Uchat.Database.Tests` содержит `ChatRoomServiceTests` и `FriendshipServiceTests`, которые мокают репозитории и проверяют защиту от некорректных акторов/состояний до обращения к базе.

### Пример использования

```csharp
public class ChatService
{
    private readonly UchatDbContext _sqliteContext;     // SQLite
    private readonly LiteDbContext _liteDbContext;      // LiteDB
    
    public ChatService(UchatDbContext sqliteContext, LiteDbContext liteDbContext)
    {
        _sqliteContext = sqliteContext;
        _liteDbContext = liteDbContext;
    }
    
    public async Task SendMessageAsync(int chatId, int userId, string content)
    {
        // 1. Получить данные пользователя из SQLite
        var user = await _sqliteContext.Users.FindAsync(userId);
        
        // 2. Сохранить сообщение в LiteDB
        var message = new LiteDbMessage
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
        
        _liteDbContext.Messages.Insert(message);
        
        // 3. Обновить LastActivityAt в SQLite
        var chatRoom = await _sqliteContext.ChatRooms.FindAsync(chatId);
        chatRoom.LastActivityAt = DateTime.UtcNow;
        await _sqliteContext.SaveChangesAsync();
    }
}
```

---

## Проверка подключения

```csharp
// SQLite
using (var scope = app.Services.CreateScope())
{
    var sqliteContext = scope.ServiceProvider.GetRequiredService<UchatDbContext>();
    var canConnect = await sqliteContext.Database.CanConnectAsync();
    Console.WriteLine($"SQLite: {(canConnect ? "✅ Connected" : "❌ Failed")}");
}

// LiteDB
var liteDbContext = app.Services.GetRequiredService<LiteDbContext>();
var liteDbExists = liteDbContext.DatabaseExists();
Console.WriteLine($"LiteDB: {(liteDbExists ? "✅ Database exists" : "❌ Database not found")}");
```

---

## 📋 Чеклист настройки

- [x] SQLite не требует установки сервера
- [x] LiteDB не требует установки сервера
- [x] Обновлен `.config/appsettings.json`
- [ ] Применены миграции: `dotnet ef database update`
- [ ] Проверено подключение к SQLite
- [ ] Проверено подключение к LiteDB
- [ ] Настроен Dependency Injection в `Program.cs`

---

## 🔒 Безопасность

⚠️ **Никогда не коммитьте `appsettings.json` с паролями в Git!**

Для локальной разработки используйте **User Secrets**:

```bash
cd Uchat.Database
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:SQLite" "Data Source=uchat.db"
dotnet user-secrets set "LiteDb:DatabasePath" "Data/messages.db"
```

Для production используйте **переменные окружения**.

---

## 🗑️ Автоудаление и бэкапы

LiteDB не поддерживает TTL индексы (как MongoDB), поэтому реализованы:

1. `MessageCleanupService` – `BackgroundService`, который через `LiteDbWriteGate` блокирует запись, открывает временное подключение `ConnectionType.Shared`, удаляет документы старше `LiteDb:RetentionDays` и ждёт `LiteDb:CleanupIntervalMinutes`.
2. `LiteDbBackupService` – `BackgroundService`, который ежесуточно (или на любом другом интервале) копирует файл `messages.db` в `LiteDb:BackupDirectory/messages-{timestamp}.db.bak`, вызывает `ILiteDbBackupUploader` для опциональной загрузки и оставляет только `LiteDb:BackupRetention` последних копий.

Оба используют `ILiteDbWriteGate`, чтобы на время операции приостановить мутации сообщений.

```csharp
builder.Services.Configure<LiteDbSettings>(builder.Configuration.GetSection("LiteDb"));
builder.Services.AddSingleton<ILiteDbWriteGate, LiteDbWriteGate>();
builder.Services.AddSingleton<LiteDbContext>();
builder.Services.AddSingleton<ILiteDbBackupUploader, NoOpLiteDbBackupUploader>();
builder.Services.AddHostedService<MessageCleanupService>();
builder.Services.AddHostedService<LiteDbBackupService>();
builder.Services.AddScoped<IMessagingCoordinator, MessagingCoordinator>();
```

Чтобы восстановить копию, вызовите `LiteDbBackupService.RestoreAsync("messages-20251125000000.db.bak")` до запуска приложения (или в рамках CLI/административной команды), предварительно остановив все хосты или дождитесь, пока `ILiteDbWriteGate` освободится.

Шардирование (`EnableSharding = true`) пригодится на высокой нагрузке: создавайте `LiteDbContext` на лету с файлом по шаблону `ShardFilePattern.Replace("{chatId}", chatId.ToString())`, а репозитории для конкретного чата используют фабрику, чтобы работать с нужным файлом сообщений.

---

## Сравнение: Облачные vs Локальные базы данных

| Характеристика | PostgreSQL (Supabase) | SQLite |
|----------------|----------------------|---------|
| Установка | Облачный сервис | Один файл |
| Масштабирование | Отличное | Ограниченное |
| Стоимость | Платный | Бесплатный |
| Скорость | Сетевые задержки | Очень быстрый |
| Подходит для | Production | Разработка, малые проекты |

| Характеристика | MongoDB Atlas | LiteDB |
|----------------|---------------|---------|
| Установка | Облачный сервис | Один файл |
| Масштабирование | Отличное | До 2 ТБ |
| Стоимость | Платный | Бесплатный |
| Скорость | Сетевые задержки | Очень быстрый |
| Подходит для | Production | Разработка, малые проекты |

**Вывод:** SQLite + LiteDB идеальны для разработки и малых проектов. Для production с большой нагрузкой лучше использовать PostgreSQL + MongoDB.
