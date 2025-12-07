using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Uchat.Database.MongoDB;

public class MongoMessage
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("chatId")]
    public int ChatId { get; set; }

    [BsonElement("sender")]
    public MessageSender Sender { get; set; } = null!;

    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;
    
    // - “text” - regular text message
    [BsonElement("type")]
    public string Type { get; set; } = "text";
    
    /// {
    ///   "😎": [100, 200, 300],  // userIds who placed 😎
    ///   "❤️": [150, 250],       
    ///   "😨": [100]             
    /// }
    [BsonElement("reactions")]
    public Dictionary<string, List<int>> Reactions { get; set; } = new();
    
    [BsonElement("readBy")]
    public List<int> ReadBy { get; set; } = new();
    
    [BsonElement("sentAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    

    // NULL = the message has not been edited
    // NOT NULL = the message has been modified
    [BsonElement("editedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? EditedAt { get; set; }
    

    // false = message is visible
    // true = message is hidden (but not physically deleted)
    [BsonElement("isDeleted")]
    public bool IsDeleted { get; set; }
    
    [BsonElement("replyToMessageId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ReplyToMessageId { get; set; }
    
    [BsonElement("replyToContent")]
    public string? ReplyToContent { get; set; }
}

public class MessageSender
{
    [BsonElement("userId")]
    public int UserId { get; set; }

    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [BsonElement("avatarUrl")]
    public string? AvatarUrl { get; set; }
}
