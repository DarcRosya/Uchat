using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Uchat.Database.LiteDB;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Database.Services.Messaging;

namespace Uchat.Server.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ILiteDbWriteGate _writeGate;
    private readonly LiteDbContext _liteDbContext;
    private readonly IMessageRepository _messageRepository;
    private readonly IChatRoomRepository _chatRoomRepository;

    public ChatHub(
        ILiteDbWriteGate writeGate, 
        LiteDbContext liteDbContext,
        IMessageRepository messageRepository,
        IChatRoomRepository chatRoomRepository)
    {
        _writeGate = writeGate;
        _liteDbContext = liteDbContext;
        _messageRepository = messageRepository;
        _chatRoomRepository = chatRoomRepository;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var username = GetUsername();
        Logger.Write($"User {username} (ID: {userId}) connected");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var username = GetUsername();
        Logger.Write($"User {username} disconnected");
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        Logger.Write($"{GetUsername()} joined group: {groupName}");
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        Logger.Write($"{GetUsername()} left group: {groupName}");
    }

    public async Task NewUserNotification(string chatId, string user)
    {
        await Clients.Group(chatId).SendAsync("ReceiveMessage", chatId, "System", $"{user} joined the chat");
        Logger.Write($"{user} joined chat {chatId}");
    }

    public async Task SendMessage(string chatId, string user, string message, string? replyContent = null)
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

        // Сохранить сообщение в LiteDB
        var messageId = await SaveMessageToLiteDb(chatId, userId, username, message, replyContent);

        // Отправить сообщение всем в группе (включая replyContent и messageId)
        await Clients.Group(chatId).SendAsync("ReceiveMessage", chatId, username, message, replyContent, messageId);
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

    private async Task<string> SaveMessageToLiteDb(string chatId, int senderId, string senderName, string content, string? replyContent = null)
    {
        try
        {
            using (await _writeGate.AcquireAsync())
            {
                // Если есть replyContent, находим ID последнего сообщения с таким текстом
                string? replyToId = null;
                if (!string.IsNullOrEmpty(replyContent))
                {
                    var recentMessages = _liteDbContext.Messages
                        .Find(m => m.ChatId == int.Parse(chatId) && m.Content == replyContent)
                        .OrderByDescending(m => m.SentAt)
                        .Take(1)
                        .FirstOrDefault();
                    
                    replyToId = recentMessages?.Id;
                }
                
                var message = new LiteDbMessage
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
                    // Сохраняем ID исходного сообщения
                    ReplyToMessageId = replyToId,
                    // Дублируем текст для быстрого отображения (денормализация)
                    ReplyToContent = replyContent
                };

                _liteDbContext.Messages.Insert(message);
                return message.Id; // Возвращаем ID вставленного сообщения
            }
        }
        catch (Exception)
        {
            // Ошибка сохранения сообщения
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
            using (await _writeGate.AcquireAsync())
            {
                var message = _liteDbContext.Messages.FindById(messageId);
                
                if (message == null || message.Sender.UserId != userId)
                    return; // Можно редактировать только свои сообщения

                message.Content = newContent;
                message.EditedAt = DateTime.UtcNow;
                
                _liteDbContext.Messages.Update(message);

                // Уведомляем всех в чате об изменении
                await Clients.Group(message.ChatId.ToString()).SendAsync(
                    "MessageEdited", 
                    messageId, 
                    newContent, 
                    message.EditedAt
                );
            }
        }
        catch (Exception ex)
        {
            Logger.Write($"Failed to edit message: {ex.Message}");
        }
    }

    public async Task DeleteMessage(string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return;

        var userId = GetUserId();

        try
        {
            using (await _writeGate.AcquireAsync())
            {
                var message = _liteDbContext.Messages.FindById(messageId);
                
                if (message == null || message.Sender.UserId != userId)
                    return; // Можно удалять только свои сообщения

                message.IsDeleted = true;
                message.Content = "Message deleted";
                
                _liteDbContext.Messages.Update(message);

                // Уведомляем всех в чате об удалении
                await Clients.Group(message.ChatId.ToString()).SendAsync(
                    "MessageDeleted", 
                    messageId
                );
            }
        }
        catch (Exception ex)
        {
            Logger.Write($"Failed to delete message: {ex.Message}");
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
