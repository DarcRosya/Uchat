namespace Uchat.Server.Services.Redis;

public static class RedisCacheKeys
{
    public const string ChatLastMessagesKey = "chat:last";

    public static string GetUserChatSortedSetKey(int userId) => $"user:{userId}:chats";
}
