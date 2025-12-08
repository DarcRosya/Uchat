using Uchat.Database.MongoDB;

namespace Uchat.Database.Repositories.Interfaces;

// Naaah, no comments here, i'm tired
public interface IMessageRepository
{
    Task<List<MongoMessage>> GetChatMessagesAsync(int chatId, DateTime? minDate, int limit = 50, DateTime? lastTimestamp = null);
    
    Task<MongoMessage?> GetMessageByIdAsync(string messageId);
    
    Task<List<MongoMessage>> GetMessagesByIdsAsync(List<string> messageIds);

    Task<List<MongoMessage>> GetUnreadMessagesAsync(int chatId, int userId);
    
    Task<long> GetUnreadCountAsync(int chatId, int userId);
    
    Task<bool> EditMessageAsync(string messageId, string newContent);
    
    Task<bool> DeleteMessagePermanentlyAsync(string messageId);
    
    Task<List<string>> ClearReplyReferencesAsync(string deletedMessageId);
    
    Task<bool> AddReactionAsync(string messageId, string emoji, int userId);
    
    Task<bool> RemoveReactionAsync(string messageId, string emoji, int userId);
    
    Task<bool> MarkAsReadAsync(string messageId, int userId);
    
    Task<long> MarkAllAsReadAsync(int chatId, int userId);
    
    Task<long> MarkAsReadUntilAsync(int chatId, int userId, DateTime untilTimestamp);
    
    Task<List<MongoMessage>> SearchMessagesAsync(int chatId, string searchQuery, int limit = 20);
}