using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Security.Claims;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Server.Services.Chat;
using Uchat.Shared;
using Uchat.Server.Services.Presence;
using Uchat.Server.Services.Reconnection;

namespace Uchat.Server.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatRoomRepository _chatRoomRepository;
    private readonly IChatRoomService _chatRoomService;
    private readonly IUserPresenceService _presenceService;
    private readonly IReconnectionService _reconnectionService;

    // Active online users: userId -> connectionIds
    private static readonly Dictionary<int, HashSet<string>> OnlineUsers = new();

    public ChatHub(
        IChatRoomRepository chatRoomRepository, 
        IChatRoomService chatRoomService, 
        IUserPresenceService presenceService,
        IReconnectionService reconnectionService)
    {
        _chatRoomRepository = chatRoomRepository;
        _chatRoomService = chatRoomService;
        _presenceService = presenceService;
        _reconnectionService = reconnectionService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var username = GetUsername();
        var connectionId = Context.ConnectionId;

        // Track connection for reconnection purposes
        var sessionId = await _reconnectionService.TrackConnectionAsync(userId, connectionId, username);
        Context.Items["SessionId"] = sessionId;

        Logger.Write($"[RECONNECTION] User {username} (ID: {userId}) connected with sessionId: {sessionId}");
        Logger.Write($"User {username} (ID: {userId}) connected");

        // Determine if this is a reconnection (user had previous connection within time window)
        bool isReconnection = false;
        var previousConnectionId = await _reconnectionService.GetPreviousConnectionIdAsync(userId);
        
        if (!string.IsNullOrEmpty(previousConnectionId) && previousConnectionId != connectionId)
        {
            isReconnection = true;
            Logger.Write($"[RECONNECTION] User {username} is reconnecting (previous: {previousConnectionId}, new: {connectionId})");
        }

        // Online logic
        bool wasOffline = false;
        lock (OnlineUsers)
        {
            if (!OnlineUsers.ContainsKey(userId))
            {
                OnlineUsers[userId] = new HashSet<string>();
                wasOffline = true;
            }

            OnlineUsers[userId].Add(connectionId);
        }

        await _presenceService.MarkOnlineAsync(userId);

        // Notify clients about reconnection or online status
        if (wasOffline)
        {
            await Clients.All.SendAsync("UserOnline", userId);
            Logger.Write($"[ONLINE] User {username} became ONLINE");
        }
        else if (isReconnection)
        {
            // User is reconnecting (was briefly offline)
            await Clients.All.SendAsync("UserReconnected", userId);
            Logger.Write($"[RECONNECTION] User {username} successfully reconnected");
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

        // Mark as reconnected in tracking service
        if (isReconnection)
        {
            await _reconnectionService.MarkReconnectedAsync(userId, connectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        var username = GetUsername();
        var connectionId = Context.ConnectionId;

        Logger.Write($"[DISCONNECT] User {username} (ID: {userId}) disconnected");
        
        if (exception != null)
        {
            Logger.Write($"[DISCONNECT] Disconnect reason: {exception.Message}");
        }

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
            await _presenceService.MarkOfflineAsync(userId);

            // Clear reconnection state only after user fully goes offline
            // (we keep it for a grace period in case they reconnect quickly)
            _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(async _ =>
            {
                await _reconnectionService.ClearReconnectionStateAsync(userId);
                Logger.Write($"[RECONNECTION] Cleared reconnection state for user {username}");
            });
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

    public async Task LeaveChatGroup(int chatId)
    {
        string groupName = $"chat_{chatId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        Logger.Write($"[LeaveChatGroup] {GetUsername()} left chat {chatId}");
    }

    public Task Heartbeat()
    {
        var userId = GetUserId();
        if (userId == 0)
        {
            return Task.CompletedTask;
        }

        return _presenceService.RefreshAsync(userId);
    }

    /// Ping-Pong heartbeat - client sends ping, server responds with pong
    /// Helps detect dead connections faster than default timeouts
    public Task Ping()
    {
        var userId = GetUserId();
        if (userId == 0)
        {
            return Task.CompletedTask;
        }

        // Refresh presence on every ping
        _ = _presenceService.RefreshAsync(userId);

        return Clients.Caller.SendAsync("Pong");
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
