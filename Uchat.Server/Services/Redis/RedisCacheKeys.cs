namespace Uchat.Server.Services.Redis;

public static class RedisCacheKeys
{
    public const string ChatLastMessagesKey = "chat:last";

    public static string GetUserChatSortedSetKey(int userId) => $"user:{userId}:chats";
    public static readonly TimeSpan ChatListSortedSetTtl = TimeSpan.FromDays(7);
    public static string GetPresenceKey(int userId) => $"presence:user:{userId}";
    public static string GetChatUnreadKey(int chatId, int userId) => $"chat:{chatId}:unread:{userId}";
}
