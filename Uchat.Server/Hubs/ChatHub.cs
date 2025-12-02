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

    public async Task SendMessage(string chatId, string user, string message)
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
        await SaveMessageToLiteDb(chatId, userId, username, message);

        // Отправить сообщение всем в группе
        await Clients.Group(chatId).SendAsync("ReceiveMessage", chatId, username, message);
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
            isDeleted = m.IsDeleted
        }).Cast<object>().ToList();
    }

    private async Task SaveMessageToLiteDb(string chatId, int senderId, string senderName, string content)
    {
        try
        {
            using (await _writeGate.AcquireAsync())
            {
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
                    IsDeleted = false
                };

                _liteDbContext.Messages.Insert(message);
            }
        }
        catch (Exception)
        {
            // Ошибка сохранения сообщения
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
