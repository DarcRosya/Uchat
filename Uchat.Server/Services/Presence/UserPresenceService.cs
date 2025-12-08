using System;
using Microsoft.Extensions.Options;
using Uchat.Server.Services.Redis;

namespace Uchat.Server.Services.Presence;

public sealed class UserPresenceService : IUserPresenceService
{
    private readonly IRedisService _redisService;
    private readonly TimeSpan _presenceTtl;
    private const string PresenceValue = "Online";

    public UserPresenceService(IRedisService redisService, IOptions<RedisSettings> options)
    {
        _redisService = redisService;
        var settings = options.Value;
        _presenceTtl = settings.PresenceTtlSeconds > 0
            ? TimeSpan.FromSeconds(settings.PresenceTtlSeconds)
            : TimeSpan.FromSeconds(60);
    }

    public Task MarkOnlineAsync(int userId)
        => _redisService.SetPresenceAsync(RedisCacheKeys.GetPresenceKey(userId), PresenceValue, _presenceTtl);

    public Task RefreshAsync(int userId)
        => _redisService.RefreshPresenceAsync(RedisCacheKeys.GetPresenceKey(userId), _presenceTtl);

    public Task MarkOfflineAsync(int userId)
        => _redisService.KeyDeleteAsync(RedisCacheKeys.GetPresenceKey(userId));

    public Task<bool> IsOnlineAsync(int userId)
        => _redisService.IsOnlineAsync(RedisCacheKeys.GetPresenceKey(userId));
}
