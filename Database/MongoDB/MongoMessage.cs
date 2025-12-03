using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Uchat.Database.MongoDB;

/// <summary>
/// Представляет документ в коллекции "messages"
/// 
/// Каждый документ содержит:
/// - Метаданные (chatId, senderId, sentAt)
/// - Контент (content, type, attachments)
/// - Вложенные данные (sender info, reactions, readBy)
/// </summary>
public class MongoMessage
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    
    [BsonElement("chatId")]
    public int ChatId { get; set; }
    
    /// <summary>
    /// Вложенный документ отправителя
    /// 
    /// Это КОПИЯ данных из Users таблицы (SQLite)
    /// Обновляется при отправке сообщения
    /// 
    /// Пример:
    /// {
    ///   "sender": {
    ///     "userId": 100,
    ///     "username": "alice",
    ///     "displayName": "Alice Smith",
    ///     "avatarUrl": "/avatars/alice.jpg"
    ///   }
    /// }
    /// </summary>
    [BsonElement("sender")]
    public MessageSender Sender { get; set; } = null!;
    
    /// <summary>
    /// Текст сообщения
    /// 
    /// Для текстовых сообщений: "Hello world!"
    /// Для медиа: может быть пустым (если только картинка)
    /// Для системных: "Alice joined the chat"
    /// </summary>
    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Тип сообщения
    /// В MongoDB: type (string)
    /// 
    /// Возможные значения:
    /// - "text" - обычное текстовое сообщение
    /// - "image" - картинка
    /// - "video" - видео
    /// - "file" - файл
    /// - "voice" - голосовое сообщение
    /// - "poll" - опрос
    /// - "system" - системное ("Alice joined")
    /// </summary>
    [BsonElement("type")]
    public string Type { get; set; } = "text";
    
    [BsonElement("attachments")]
    public List<MessageAttachment> Attachments { get; set; } = new();
    
    /// <summary>
    /// Реакции на сообщение
    /// В MongoDB: reactions (object)
    /// 
    /// Структура:
    /// {
    ///   "👍": [100, 200, 300],  // userIds кто поставил 👍
    ///   "❤️": [150, 250],       // userIds кто поставил ❤️
    ///   "😂": [100]             // userIds кто поставил 😂
    /// }
    /// 
    /// Атомарное обновление:
    /// - Добавить: $addToSet
    /// - Удалить: $pull
    /// </summary>
    [BsonElement("reactions")]
    public Dictionary<string, List<int>> Reactions { get; set; } = new();
    
    [BsonElement("readBy")]
    public List<int> ReadBy { get; set; } = new();
    
    [BsonElement("sentAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Дата и время последнего редактирования (UTC)
    /// 
    /// NULL = сообщение не редактировалось
    /// NOT NULL = сообщение было изменено
    /// </summary>
    [BsonElement("editedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? EditedAt { get; set; }
    
    /// <summary>
    /// Флаг "сообщение удалено"
    /// 
    /// false = сообщение видно
    /// true = сообщение скрыто (но не удалено физически)
    /// </summary>
    [BsonElement("isDeleted")]
    public bool IsDeleted { get; set; }
    
    [BsonElement("replyToMessageId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ReplyToMessageId { get; set; }
    
    [BsonElement("replyToContent")]
    public string? ReplyToContent { get; set; }
}

// ============================================================================
// ВЛОЖЕННЫЕ КЛАССЫ (Embedded Documents)
// ============================================================================

/// <summary>
/// Вложенный документ отправителя сообщения
/// 
/// Это КОПИЯ данных из Users (SQLite)
/// Обновляется при отправке сообщения
/// </summary>
public class MessageSender
{
    /// <summary>
    /// ID пользователя из Users.Id (SQLite)
    /// </summary>
    [BsonElement("userId")]
    public int UserId { get; set; }
    
    /// <summary>
    /// Username из Users.Username (SQLite)
    /// Копия на момент отправки сообщения
    /// </summary>
    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Отображаемое имя из Users.DisplayName (SQLite)
    /// </summary>
    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// URL аватара из Users.AvatarUrl (SQLite)
    /// NULL = аватар по умолчанию
    /// </summary>
    [BsonElement("avatarUrl")]
    public string? AvatarUrl { get; set; }
}

/// <summary>
/// Вложение к сообщению (фото, видео, файл)
/// В MongoDB: встроен в messages.attachments[]
/// </summary>
public class MessageAttachment
{
    /// <summary>
    /// Тип вложения
    /// "image" | "video" | "file" | "voice" | "audio"
    /// </summary>
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// URL файла
    /// Пример: "/uploads/2024/01/photo_12345.jpg"
    /// </summary>
    [BsonElement("url")]
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// Размер файла в байтах
    /// Используется для проверки лимитов и отображения
    /// </summary>
    [BsonElement("size")]
    public long Size { get; set; }
    
    /// <summary>
    /// Оригинальное имя файла
    /// Пример: "vacation_photo.jpg"
    /// </summary>
    [BsonElement("fileName")]
    public string? FileName { get; set; }
    
    // ========================================================================
    // ДЛЯ ИЗОБРАЖЕНИЙ И ВИДЕО
    // ========================================================================
    
    /// <summary>
    /// Ширина изображения/видео в пикселях
    /// NULL для файлов и аудио
    /// </summary>
    [BsonElement("width")]
    public int? Width { get; set; }
    
    /// <summary>
    /// Высота изображения/видео в пикселях
    /// NULL для файлов и аудио
    /// </summary>
    [BsonElement("height")]
    public int? Height { get; set; }
    
    /// <summary>
    /// Длительность видео/аудио в секундах
    /// NULL для изображений и файлов
    /// </summary>
    [BsonElement("duration")]
    public int? Duration { get; set; }
    
    /// <summary>
    /// URL превью (thumbnail) для видео
    /// NULL для остальных типов
    /// </summary>
    [BsonElement("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }
}
