using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Server.Services.Redis;

namespace Uchat.Server.Services.Unread;

public sealed class UnreadCounterService : IUnreadCounterService
{
    private readonly IRedisService _redisService;
    private readonly IMessageRepository _messageRepository;
    private readonly TimeSpan _unreadTtl = TimeSpan.FromDays(14);

    public UnreadCounterService(IRedisService redisService, IMessageRepository messageRepository)
    {
        _redisService = redisService;
        _messageRepository = messageRepository;
    }

    public async Task IncrementUnreadAsync(int chatId, IEnumerable<int> participantIds, int excludeUserId)
    {
        if (!_redisService.IsAvailable || chatId <= 0)
        {
            return;
        }

        var targets = participantIds
            .Where(id => id != excludeUserId)
            .Distinct()
            .ToList();

        if (!targets.Any())
        {
            return;
        }

        var tasks = targets.Select(userId =>
            _redisService.IncrementAsync(RedisCacheKeys.GetChatUnreadKey(chatId, userId), 1, _unreadTtl));

        await Task.WhenAll(tasks);
    }

    public async Task<int> GetUnreadCountAsync(int? chatId, int userId)
    {
        if (!chatId.HasValue || chatId <= 0)
        {
            return 0;
        }

        if (_redisService.IsAvailable)
        {
            var raw = await _redisService.GetStringAsync(RedisCacheKeys.GetChatUnreadKey(chatId.Value, userId));
            if (int.TryParse(raw, out var parsed))
            {
                return parsed;
            }
        }

        var fallback = await _messageRepository.GetUnreadCountAsync(chatId.Value, userId);
        return (int)Math.Min(int.MaxValue, fallback);
    }

    public async Task<Dictionary<int, int>> GetUnreadCountsAsync(int userId, IEnumerable<int> chatIds)
    {
        var result = new Dictionary<int, int>();
        var unique = chatIds.Where(id => id > 0).Distinct();

        foreach (var chatId in unique)
        {
            var count = await GetUnreadCountAsync(chatId, userId);
            if (count > 0)
            {
                result[chatId] = count;
            }
        }

        return result;
    }

    public Task ResetUnreadAsync(int chatId, int userId)
    {
        if (!_redisService.IsAvailable || chatId <= 0)
        {
            return Task.CompletedTask;
        }

        return _redisService.SetStringAsync(RedisCacheKeys.GetChatUnreadKey(chatId, userId), "0", _unreadTtl);
    }
}
