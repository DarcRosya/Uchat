using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Security.Claims;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Server.Services.Chat;

namespace Uchat.Server.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatRoomRepository _chatRoomRepository;

    // Active online users
    private static readonly Dictionary<int, HashSet<string>> OnlineUsers = new();

    public ChatHub(IChatRoomRepository chatRoomRepository)
    {
        _chatRoomRepository = chatRoomRepository;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var username = GetUsername();
        var connectionId = Context.ConnectionId;

        Logger.Write($"User {username} (ID: {userId}) connected");

        // Online logic
        lock (OnlineUsers)
        {
            if (!OnlineUsers.ContainsKey(userId))
                OnlineUsers[userId] = new HashSet<string>();

            OnlineUsers[userId].Add(connectionId);
        }

        // Notify all clients if user became online
        if (OnlineUsers[userId].Count == 1)
        {
            await Clients.All.SendAsync("UserOnline", userId);
            Logger.Write($"[ONLINE] User {username} became ONLINE");
        }

        // Auto join to chatGroups
        try
        {
            var userChats = await _chatRoomRepository.GetUserChatMembershipsAsync(userId);

            foreach (var chat in userChats)
            {
                string groupName = $"chat_{chat.Id}";
                await Groups.AddToGroupAsync(connectionId, groupName);
                Logger.Write($"[Auto-Join] User {username} added to group {groupName}");
            }

            Logger.Write($"[Auto-Join] User {username} joined {userChats.Count()} chat groups");
        }
        catch (Exception ex)
        {
            Logger.Write($"[Auto-Join ERROR] {ex.Message}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        var username = GetUsername();
        var connectionId = Context.ConnectionId;

        Logger.Write($"User {username} disconnected");

        // Online logic
        bool becameOffline = false;

        lock (OnlineUsers)
        {
            if (OnlineUsers.ContainsKey(userId))
            {
                OnlineUsers[userId].Remove(connectionId);

                if (OnlineUsers[userId].Count == 0)
                {
                    OnlineUsers.Remove(userId);
                    becameOffline = true;
                }
            }
        }

        if (becameOffline)
        {
            await Clients.All.SendAsync("UserOffline", userId);
            Logger.Write($"[OFFLINE] User {username} became OFFLINE");
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Groups
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        Logger.Write($"[JoinGroup] {GetUsername()} joined {groupName}");
    }

    public async Task JoinChatGroup(int chatId)
    {
        string groupName = $"chat_{chatId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        Logger.Write($"[JoinChatGroup] {GetUsername()} joined chat {chatId} (group: {groupName})");
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        Logger.Write($"[LeaveGroup] {GetUsername()} left {groupName}");
    }
    
    // Getters
    private int GetUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    private string GetUsername()
    {
        return Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
    }
}
