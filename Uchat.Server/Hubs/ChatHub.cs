using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using MongoDB.Bson;
using MongoDB.Driver;
using Uchat.Database.MongoDB;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Database.Services.Messaging;

namespace Uchat.Server.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly MongoDbContext _mongoDbContext;
    private readonly IMessageRepository _messageRepository;
    private readonly IChatRoomRepository _chatRoomRepository;

    public ChatHub(
        MongoDbContext mongoDbContext,
        IMessageRepository messageRepository,
        IChatRoomRepository chatRoomRepository)
    {
        _mongoDbContext = mongoDbContext;
        _messageRepository = messageRepository;
        _chatRoomRepository = chatRoomRepository;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var username = GetUsername();
        Console.WriteLine($"User {username} (ID: {userId}) connected");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var username = GetUsername();
        Console.WriteLine($"User {username} disconnected");
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        Console.WriteLine($"{GetUsername()} joined group: {groupName}");
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        Console.WriteLine($"{GetUsername()} left group: {groupName}");
    }

    public async Task NewUserNotification(string chatId, string user)
    {
        await Clients.Group(chatId).SendAsync("ReceiveMessage", chatId, "System", $"{user} joined the chat");
        Console.WriteLine($"{user} joined chat {chatId}");
    }

    public async Task SendMessage(string chatId, string user, string message, string? replyToMessageId = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var userId = GetUserId();
        var username = GetUsername();
        
        // Проверяем, что пользователь является участником чата
        if (int.TryParse(chatId, out var chatRoomId))
        {
            var chatRoom = await _chatRoomRepository.GetByIdAsync(chatRoomId);
            if (chatRoom == null || !chatRoom.Members.Any(m => m.UserId == userId))
            {
                return;
            }
        }

        // Получаем текст сообщения, на которое отвечаем (для обратной совместимости с UI)
        string? replyContent = null;
        if (!string.IsNullOrEmpty(replyToMessageId))
        {
            var filter = Builders<MongoMessage>.Filter.Eq(m => m.Id, replyToMessageId);
            var replyMessage = await _mongoDbContext.Messages.Find(filter).FirstOrDefaultAsync();
            replyContent = replyMessage?.Content;
        }

        // Сохранить сообщение в MongoDB
        var messageId = await SaveMessageToMongo(chatId, userId, username, message, replyToMessageId);

        // Отправить сообщение всем в группе (включая replyContent, messageId и replyToMessageId)
        await Clients.Group(chatId).SendAsync("ReceiveMessage", chatId, username, message, replyContent, messageId, replyToMessageId);
    }
    
    public async Task<List<object>> GetChatHistory(string chatId, int limit = 50)
    {
        if (!int.TryParse(chatId, out var chatRoomId))
            return new List<object>();
            
        var userId = GetUserId();
        
        // Проверяем, что пользователь является участником чата
        var chatRoom = await _chatRoomRepository.GetByIdAsync(chatRoomId);
        if (chatRoom == null || !chatRoom.Members.Any(m => m.UserId == userId))
        {
            return new List<object>();
        }
        
        var messages = await _messageRepository.GetChatMessagesAsync(chatRoomId, limit);
        
        // Конвертируем в простые объекты для сериализации
        return messages.Select(m => new
        {
            id = m.Id,
            chatId = m.ChatId,
            sender = new
            {
                userId = m.Sender.UserId,
                username = m.Sender.Username,
                displayName = m.Sender.DisplayName,
                avatarUrl = m.Sender.AvatarUrl
            },
            content = m.Content,
            type = m.Type,
            sentAt = m.SentAt,
            editedAt = m.EditedAt,
            isDeleted = m.IsDeleted,
            replyToMessageId = m.ReplyToMessageId, // Для будущей функциональности
            replyToContent = m.ReplyToContent // Текст сообщения, на которое ответили
        }).Cast<object>().ToList();
    }

    private async Task<string> SaveMessageToMongo(string chatId, int senderId, string senderName, string content, string? replyToMessageId = null)
    {
        try
        {
            // Получаем текст исходного сообщения для ReplyToContent
            string? replyContent = null;
            if (!string.IsNullOrEmpty(replyToMessageId))
            {
                var filter = Builders<MongoMessage>.Filter.Eq(m => m.Id, replyToMessageId);
                var replyMessage = await _mongoDbContext.Messages.Find(filter).FirstOrDefaultAsync();
                replyContent = replyMessage?.Content;
            }
            
            var message = new MongoMessage
            {
                ChatId = int.TryParse(chatId, out var cId) ? cId : 0,
                Sender = new MessageSender
                {
                    UserId = senderId,
                    Username = senderName,
                    DisplayName = senderName,
                    AvatarUrl = null
                },
                Content = content,
                Type = "text",
                SentAt = DateTime.UtcNow,
                EditedAt = null,
                IsDeleted = false,
                ReplyToMessageId = replyToMessageId,
                ReplyToContent = replyContent
            };

            await _mongoDbContext.Messages.InsertOneAsync(message);
            return message.Id;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public async Task EditMessage(string messageId, string newContent)
    {
        if (string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(newContent))
            return;

        var userId = GetUserId();

        try
        {
            var filter = Builders<MongoMessage>.Filter.Eq(m => m.Id, messageId);
            var message = await _mongoDbContext.Messages.Find(filter).FirstOrDefaultAsync();
            
            if (message == null || message.Sender.UserId != userId)
                return;

            var update = Builders<MongoMessage>.Update
                .Set(m => m.Content, newContent)
                .Set(m => m.EditedAt, DateTime.UtcNow);
            
            await _mongoDbContext.Messages.UpdateOneAsync(filter, update);

            await Clients.Group(message.ChatId.ToString()).SendAsync(
                "MessageEdited", 
                messageId, 
                newContent, 
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to edit message: {ex.Message}");
        }
    }

    public async Task DeleteMessage(string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return;

        var userId = GetUserId();
        var username = GetUsername();

        Console.WriteLine($"[DeleteMessage] User {username} (ID: {userId}) attempting to delete message: {messageId}");

        try
        {
            var filter = Builders<MongoMessage>.Filter.Eq(m => m.Id, messageId);
            var message = await _mongoDbContext.Messages.Find(filter).FirstOrDefaultAsync();
            
            if (message == null)
            {
                Console.WriteLine($"[DeleteMessage] Message {messageId} not found");
                return;
            }
            
            if (message.Sender.UserId != userId)
            {
                Console.WriteLine($"[DeleteMessage] Permission denied: User {userId} cannot delete message from user {message.Sender.UserId}");
                return;
            }

            Console.WriteLine($"[DeleteMessage] Deleting message {messageId} in chat {message.ChatId}");

            // Удаляем само сообщение
            var update = Builders<MongoMessage>.Update
                .Set(m => m.IsDeleted, true)
                .Set(m => m.Content, "Message deleted");
            
            await _mongoDbContext.Messages.UpdateOneAsync(filter, update);

            // Находим все сообщения, которые отвечают на удалённое
            var replyFilter = Builders<MongoMessage>.Filter.Eq(m => m.ReplyToMessageId, messageId);
            var repliesUpdate = Builders<MongoMessage>.Update
                .Set(m => m.ReplyToMessageId, null)
                .Set(m => m.ReplyToContent, null);
            
            var repliesResult = await _mongoDbContext.Messages.UpdateManyAsync(replyFilter, repliesUpdate);
            Console.WriteLine($"[DeleteMessage] Cleared {repliesResult.ModifiedCount} reply references");

            var hasReplies = repliesResult.ModifiedCount > 0;
            
            // Отправляем событие в группу
            await Clients.Group(message.ChatId.ToString()).SendAsync(
                "MessageDeletedWithReplies",
                messageId,
                hasReplies
            );
            
            Console.WriteLine($"[DeleteMessage] Sent MessageDeletedWithReplies event to group for {messageId}, hasReplies: {hasReplies}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DeleteMessage] Failed to delete message: {ex.Message}");
        }
    }

    private int GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

    private string GetUsername()
    {
        return Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
    }
}
