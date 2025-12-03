# ============================================================================
# REDIS SETUP GUIDE - Uchat
# ============================================================================

## 📦 Установка и запуск Redis

### Вариант 1: Docker (рекомендуется)

```bash
# Запустить Redis контейнер
docker-compose up -d

# Проверить статус
docker-compose ps

# Просмотреть логи
docker-compose logs -f redis

# Остановить
docker-compose down
```

### Вариант 2: Локальная установка (Windows)

Скачать Redis для Windows:
- https://github.com/tporadowski/redis/releases
- Или через Chocolatey: `choco install redis-64`

---

## 🔧 Подключение в C#

### 1. Установить NuGet пакет

```bash
dotnet add package StackExchange.Redis
```

### 2. Настройка в appsettings.json

Уже настроено в `.config/appsettings.json`:

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "Uchat:",
    "AbortOnConnectFail": false,
    "ConnectTimeout": 5000,
    "SyncTimeout": 5000,
    "DefaultDatabase": 0
  }
}
```

### 3. Регистрация в Program.cs

```csharp
using StackExchange.Redis;

// Конфигурация Redis
var redisConfig = builder.Configuration.GetSection("Redis");
var connectionString = redisConfig["ConnectionString"];

// Создание подключения (Singleton)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(connectionString!);
    configuration.AbortOnConnectFail = bool.Parse(redisConfig["AbortOnConnectFail"] ?? "false");
    configuration.ConnectTimeout = int.Parse(redisConfig["ConnectTimeout"] ?? "5000");
    configuration.SyncTimeout = int.Parse(redisConfig["SyncTimeout"] ?? "5000");
    
    return ConnectionMultiplexer.Connect(configuration);
});

// Добавить Redis Distributed Cache (опционально)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = connectionString;
    options.InstanceName = redisConfig["InstanceName"];
});
```

---

## 💡 Примеры использования

### Базовые операции

```csharp
public class RedisCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    
    public RedisCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _db = redis.GetDatabase();
    }
    
    // Установить значение с TTL
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var json = JsonSerializer.Serialize(value);
        await _db.StringSetAsync(key, json, expiry);
    }
    
    // Получить значение
    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _db.StringGetAsync(key);
        if (!value.HasValue)
            return default;
        
        return JsonSerializer.Deserialize<T>(value!);
    }
    
    // Удалить ключ
    public async Task<bool> DeleteAsync(string key)
    {
        return await _db.KeyDeleteAsync(key);
    }
    
    // Проверить существование
    public async Task<bool> ExistsAsync(string key)
    {
        return await _db.KeyExistsAsync(key);
    }
}
```

### Кеширование пользовательских данных

```csharp
public class UserService
{
    private readonly RedisCacheService _cache;
    private readonly IUserRepository _userRepo;
    
    public async Task<User?> GetUserByIdAsync(int userId)
    {
        var cacheKey = $"user:{userId}";
        
        // Попытка получить из кеша
        var cachedUser = await _cache.GetAsync<User>(cacheKey);
        if (cachedUser != null)
            return cachedUser;
        
        // Если нет в кеше - загрузить из БД
        var user = await _userRepo.GetByIdAsync(userId);
        if (user != null)
        {
            // Сохранить в кеш на 1 час
            await _cache.SetAsync(cacheKey, user, TimeSpan.FromHours(1));
        }
        
        return user;
    }
    
    // Инвалидация кеша при обновлении
    public async Task UpdateUserAsync(User user)
    {
        await _userRepo.UpdateAsync(user);
        
        // Удалить из кеша
        await _cache.DeleteAsync($"user:{user.Id}");
    }
}
```

### Pub/Sub для real-time уведомлений

```csharp
public class MessageNotificationService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ISubscriber _subscriber;
    
    public MessageNotificationService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _subscriber = redis.GetSubscriber();
    }
    
    // Отправить уведомление о новом сообщении
    public async Task NotifyNewMessageAsync(int chatId, string messageId)
    {
        var channel = $"chat:{chatId}:messages";
        var payload = JsonSerializer.Serialize(new { MessageId = messageId, Timestamp = DateTime.UtcNow });
        
        await _subscriber.PublishAsync(channel, payload);
    }
    
    // Подписаться на уведомления чата
    public async Task SubscribeToChat(int chatId, Action<string> onMessageReceived)
    {
        var channel = $"chat:{chatId}:messages";
        
        await _subscriber.SubscribeAsync(channel, (ch, message) =>
        {
            onMessageReceived(message!);
        });
    }
    
    // Отписаться от чата
    public async Task UnsubscribeFromChat(int chatId)
    {
        var channel = $"chat:{chatId}:messages";
        await _subscriber.UnsubscribeAsync(channel);
    }
}
```

### Online статус пользователей

```csharp
public class PresenceService
{
    private readonly IDatabase _db;
    
    public PresenceService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }
    
    // Пометить пользователя как online
    public async Task SetUserOnlineAsync(int userId)
    {
        var key = $"presence:user:{userId}";
        // Установить с автоматическим истечением через 5 минут
        await _db.StringSetAsync(key, "online", TimeSpan.FromMinutes(5));
    }
    
    // Продлить online статус (вызывать каждую минуту)
    public async Task HeartbeatAsync(int userId)
    {
        await SetUserOnlineAsync(userId);
    }
    
    // Проверить, онлайн ли пользователь
    public async Task<bool> IsUserOnlineAsync(int userId)
    {
        var key = $"presence:user:{userId}";
        return await _db.KeyExistsAsync(key);
    }
    
    // Получить список онлайн пользователей
    public async Task<List<int>> GetOnlineUsersAsync(List<int> userIds)
    {
        var onlineUsers = new List<int>();
        
        foreach (var userId in userIds)
        {
            if (await IsUserOnlineAsync(userId))
                onlineUsers.Add(userId);
        }
        
        return onlineUsers;
    }
}
```

### Typing indicators ("печатает...")

```csharp
public class TypingIndicatorService
{
    private readonly IDatabase _db;
    private readonly ISubscriber _subscriber;
    
    public TypingIndicatorService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
        _subscriber = redis.GetSubscriber();
    }
    
    // Пользователь начал печатать
    public async Task StartTypingAsync(int chatId, int userId, string username)
    {
        var key = $"typing:chat:{chatId}:user:{userId}";
        await _db.StringSetAsync(key, username, TimeSpan.FromSeconds(5));
        
        // Уведомить других участников
        var channel = $"chat:{chatId}:typing";
        await _subscriber.PublishAsync(channel, JsonSerializer.Serialize(new 
        { 
            UserId = userId, 
            Username = username, 
            IsTyping = true 
        }));
    }
    
    // Получить список печатающих в чате
    public async Task<List<string>> GetTypingUsersAsync(int chatId)
    {
        var pattern = $"typing:chat:{chatId}:user:*";
        var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints().First());
        
        var typingUsers = new List<string>();
        foreach (var key in server.Keys(pattern: pattern))
        {
            var username = await _db.StringGetAsync(key);
            if (username.HasValue)
                typingUsers.Add(username!);
        }
        
        return typingUsers;
    }
}
```

---

## 📊 Мониторинг

### Redis CLI команды

```bash
# Подключиться к Redis
docker-compose exec redis redis-cli

# Информация о сервере
INFO

# Статистика
INFO stats

# Использование памяти
INFO memory

# Количество ключей
DBSIZE

# Список всех ключей (только для dev!)
KEYS *

# Получить значение
GET user:100:name

# Удалить ключ
DEL user:100:name

# Проверить TTL
TTL user:100:name
```

---

## 🎯 Когда использовать Redis

### ✅ Используй для:
- Кеш часто запрашиваемых данных (профили пользователей, контакты)
- Online/offline статусы
- Typing indicators
- Rate limiting (ограничение частоты запросов)
- Сессии и токены
- Pub/Sub уведомления
- Счетчики (непрочитанные сообщения)

### ❌ НЕ используй для:
- Основное хранилище данных (используй SQLite)
- История сообщений (используй LiteDB)
- Данные, требующие гарантированной персистентности
- Сложные запросы с JOIN

---

## 🔒 Безопасность

Для production рекомендуется:

```yaml
# В docker-compose.yml добавить пароль:
command: >
  redis-server
  --requirepass your_strong_password
  --appendonly yes
```

В appsettings.json:
```json
{
  "Redis": {
    "ConnectionString": "localhost:6379,password=your_strong_password"
  }
}
```

---

## 📚 Полезные ссылки

- StackExchange.Redis документация: https://stackexchange.github.io/StackExchange.Redis/
- Redis команды: https://redis.io/commands
- Best practices: https://redis.io/docs/manual/patterns/

---

## 🚀 Быстрый старт

```bash
# 1. Запустить Redis
docker-compose up -d

# 2. Добавить NuGet пакет в проект
cd Uchat.Database
dotnet add package StackExchange.Redis

# 3. Проверить подключение
docker-compose exec redis redis-cli ping
# Должен вернуть: PONG
```

Готово! Redis настроен и готов к использованию. 🎉
