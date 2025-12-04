using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Server.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatRoomRepository _chatRoomRepository;

    public ChatHub(IChatRoomRepository chatRoomRepository)
    {
        _chatRoomRepository = chatRoomRepository;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var username = GetUsername();
        Logger.Write($"User {username} (ID: {userId}) connected");

        // Автоматически присоединяем пользователя ко всем его чатам
        // После миграции на Redis: это будет заменено на Redis Pub/Sub подписки
        try
        {
            var userChats = await _chatRoomRepository.GetUserChatRoomsAsync(userId);
            
            foreach (var chat in userChats)
            {
                var groupName = $"chat_{chat.Id}";
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                Logger.Write($"[Auto-Join] User {username} added to group: {groupName}");
            }
            
            Logger.Write($"[Auto-Join] User {username} joined {userChats.Count()} chat groups");
        }
        catch (Exception ex)
        {
            Logger.Write($"[Auto-Join ERROR] Failed to join user {username} to chats: {ex.Message}");
        }

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
        var username = GetUsername();
        Logger.Write($"[JoinGroup] User '{username}' (ConnectionId: {Context.ConnectionId}) joined group: {groupName}");
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        var username = GetUsername();
        Logger.Write($"[LeaveGroup] User '{username}' (ConnectionId: {Context.ConnectionId}) left group: {groupName}");
    }

    public async Task NewUserNotification(string chatId, string user)
    {
        await Clients.Group(chatId).SendAsync("ReceiveMessage", chatId, "System", $"{user} joined the chat");
        Logger.Write($"{user} joined chat {chatId}");
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
