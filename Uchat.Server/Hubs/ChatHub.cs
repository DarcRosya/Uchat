using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Uchat.Database.LiteDB;
using Uchat.Database.Services.Messaging;

namespace Uchat.Server.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ILiteDbWriteGate _writeGate;
    private readonly LiteDbContext _liteDbContext;

    public ChatHub(ILiteDbWriteGate writeGate, LiteDbContext liteDbContext)
    {
        _writeGate = writeGate;
        _liteDbContext = liteDbContext;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var username = GetUsername();
        
        Console.WriteLine($"User {username} (ID: {userId}) connected to ChatHub");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var username = GetUsername();
        Console.WriteLine($"User {username} disconnected from ChatHub");
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        var username = GetUsername();
        Console.WriteLine($"{username} joined group: {groupName}");
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        var username = GetUsername();
        Console.WriteLine($"{username} left group: {groupName}");
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

        // Сохранить сообщение в LiteDB
        await SaveMessageToLiteDb(chatId, userId, username, message);

        // Отправить сообщение всем в группе
        await Clients.Group(chatId).SendAsync("ReceiveMessage", chatId, username, message);

        Console.WriteLine($"[{chatId}] {user}: {message}");
    }

    private async Task SaveMessageToLiteDb(string chatId, int senderId, string senderName, string content)
    {
        try
        {
            using (await _writeGate.AcquireAsync())
            {
                var collection = _liteDbContext.Messages;
                
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

                collection.Insert(message);
                Console.WriteLine($"Message saved to LiteDB: {message.Id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving message to LiteDB: {ex.Message}");
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
