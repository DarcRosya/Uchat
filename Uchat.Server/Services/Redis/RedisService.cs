using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Uchat.Server.Services.Redis;

public sealed class RedisService : IRedisService
{
    private readonly ILogger<RedisService> _logger;
    private readonly RedisSettings _settings;
    private readonly string _keyPrefix;
    private readonly TimeSpan? _defaultTtl;
    private readonly TimeSpan _presenceTtl;
    private ConnectionMultiplexer? _multiplexer;
    private IDatabase? _database;
    private ISubscriber? _subscriber;

    public bool IsAvailable => _database != null && _multiplexer?.IsConnected == true;

    public RedisService(IOptions<RedisSettings> options, ILogger<RedisService> logger)
    {
        _logger = logger;
        _settings = options.Value;
        _keyPrefix = BuildKeyPrefix(_settings.InstanceName, _settings.ChannelPrefix);
        _defaultTtl = _settings.DefaultEntryTtlSeconds > 0
            ? TimeSpan.FromSeconds(_settings.DefaultEntryTtlSeconds)
            : null;
        _presenceTtl = _settings.PresenceTtlSeconds > 0
            ? TimeSpan.FromSeconds(_settings.PresenceTtlSeconds)
            : TimeSpan.FromSeconds(60);

        if (string.IsNullOrWhiteSpace(_settings.ConnectionString))
        {
            _logger.LogInformation("Redis connection string is not configured; caching/presence disabled.");
            return;
        }

        try
        {
            _multiplexer = ConnectionMultiplexer.Connect(_settings.ConnectionString);
            _database = _multiplexer.GetDatabase();
            _subscriber = _multiplexer.GetSubscriber();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Redis; caching/presence features will be skipped.");
        }
    }

    public async Task SetHashAsync(string key, string field, string value, TimeSpan? ttl = null)
    {
        if (!IsAvailable)
        {
            return;
        }

        try
        {
            var fullKey = ApplyPrefix(key);
            await _database!.HashSetAsync(fullKey, field, value).ConfigureAwait(false);
            var expiry = ttl ?? _defaultTtl;
            if (expiry.HasValue)
            {
                await _database.KeyExpireAsync(fullKey, expiry).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SetHash failed for key {Key}", key);
        }
    }

    public async Task SetStringAsync(string key, string value, TimeSpan? ttl = null)
    {
        if (!IsAvailable)
        {
            return;
        }

        try
        {
            var fullKey = ApplyPrefix(key);
            await _database!.StringSetAsync(fullKey, value).ConfigureAwait(false);
            var expiry = ttl ?? _defaultTtl;
            if (expiry.HasValue)
            {
                await _database.KeyExpireAsync(fullKey, expiry).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis StringSet failed for key {Key}", key);
        }
    }

    public async Task<string?> GetStringAsync(string key)
    {
        if (!IsAvailable)
        {
            return null;
        }

        try
        {
            var raw = await _database!.StringGetAsync(ApplyPrefix(key)).ConfigureAwait(false);
            return raw.IsNull ? null : raw.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis StringGet failed for key {Key}", key);
            return null;
        }
    }

    public async Task<long> IncrementAsync(string key, long value = 1, TimeSpan? ttl = null)
    {
        if (!IsAvailable)
        {
            return 0;
        }

        try
        {
            var fullKey = ApplyPrefix(key);
            var newValue = await _database!.StringIncrementAsync(fullKey, value).ConfigureAwait(false);
            var expiry = ttl ?? _defaultTtl;
            if (expiry.HasValue)
            {
                await _database.KeyExpireAsync(fullKey, expiry).ConfigureAwait(false);
            }

            return newValue;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis StringIncrement failed for key {Key}", key);
            return 0;
        }
    }

    public async Task<string?> GetHashAsync(string key, string field)
    {
        if (!IsAvailable)
        {
            return null;
        }

        try
        {
            var value = await _database!.HashGetAsync(ApplyPrefix(key), field).ConfigureAwait(false);
            return value.IsNull ? null : value.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GetHash failed for key {Key}", key);
            return null;
        }
    }

    public async Task<Dictionary<string, string?>> GetHashAsync(string key, IEnumerable<string> fields)
    {
        if (!IsAvailable)
        {
            return new Dictionary<string, string?>();
        }

        try
        {
            var fieldList = fields.ToList();
            if (!fieldList.Any())
            {
                return new Dictionary<string, string?>();
            }

            var entries = await _database!.HashGetAsync(ApplyPrefix(key), fieldList.Select(k => (RedisValue)k).ToArray()).ConfigureAwait(false);
            var result = new Dictionary<string, string?>();
            for (var i = 0; i < fieldList.Count; i++)
            {
                var entry = entries[i];
                result[fieldList[i]] = entry.IsNull ? null : entry.ToString();
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis multi-field GetHash failed for key {Key}", key);
            return new Dictionary<string, string?>();
        }
    }

    public async Task UpdateSortedSetAsync(string key, string member, double score, TimeSpan? ttl = null)
    {
        if (!IsAvailable)
        {
            return;
        }

        try
        {
            var fullKey = ApplyPrefix(key);
            await _database!.SortedSetAddAsync(fullKey, member, score).ConfigureAwait(false);
            var expiry = ttl ?? _defaultTtl;
            if (expiry.HasValue)
            {
                await _database.KeyExpireAsync(fullKey, expiry).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SortedSetAdd failed for key {Key}", key);
        }
    }

    public async Task<IEnumerable<string>> GetSortedSetMembersAsync(string key, long start = 0, long stop = -1, Order order = Order.Descending)
    {
        if (!IsAvailable)
        {
            return Array.Empty<string>();
        }

        try
        {
            var raw = await _database!.SortedSetRangeByRankAsync(ApplyPrefix(key), start, stop, order).ConfigureAwait(false);
            return raw.Where(r => !r.IsNull).Select(r => r.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SortedSetRange failed for key {Key}", key);
            return Array.Empty<string>();
        }
    }

    public async Task<List<string>> GetSortedKeysAsync(string sortedSetKey)
    {
        var members = await GetSortedSetMembersAsync(sortedSetKey, 0, -1, Order.Descending);
        return members.ToList();
    }

    public async Task<bool> RemoveSortedSetMemberAsync(string key, string member)
    {
        if (!IsAvailable)
        {
            return false;
        }

        try
        {
            return await _database!.SortedSetRemoveAsync(ApplyPrefix(key), member).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SortedSetRemove failed for key {Key}", key);
            return false;
        }
    }

    public async Task<bool> HashDeleteAsync(string key, string field)
    {
        if (!IsAvailable)
        {
            return false;
        }

        try
        {
            return await _database!.HashDeleteAsync(ApplyPrefix(key), field).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis HashDelete failed for key {Key}", key);
            return false;
        }
    }

    public async Task<long> PublishAsync(string channel, string message)
    {
        if (!IsAvailable || _subscriber == null)
        {
            return 0;
        }

        try
        {
            var channelName = new RedisChannel(ApplyPrefix(channel) + "pub", RedisChannel.PatternMode.Literal);
            return await _subscriber.PublishAsync(channelName, message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis publish failed for channel {Channel}", channel);
            return 0;
        }
    }

    public async Task<bool> KeyDeleteAsync(string key)
    {
        if (!IsAvailable)
        {
            return false;
        }

        try
        {
            return await _database!.KeyDeleteAsync(ApplyPrefix(key)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis KeyDelete failed for key {Key}", key);
            return false;
        }
    }

    public Task SetPresenceAsync(string key, string value, TimeSpan ttl)
        => SetStringAsync(key, value, ttl);

    public async Task RefreshPresenceAsync(string key, TimeSpan ttl)
    {
        if (!IsAvailable)
        {
            return;
        }

        try
        {
            await _database!.KeyExpireAsync(ApplyPrefix(key), ttl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis RefreshPresence failed for key {Key}", key);
        }
    }

    public async Task<bool> IsOnlineAsync(string key)
    {
        if (!IsAvailable)
        {
            return false;
        }

        try
        {
            return await _database!.KeyExistsAsync(ApplyPrefix(key)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis KeyExists failed for key {Key}", key);
            return false;
        }
    }

    private static string BuildKeyPrefix(string? instanceName, string? channelPrefix)
    {
        var segments = new List<string>();
        if (!string.IsNullOrWhiteSpace(instanceName))
        {
            segments.Add(instanceName.Trim(':'));
        }

        if (!string.IsNullOrWhiteSpace(channelPrefix))
        {
            segments.Add(channelPrefix.Trim(':'));
        }

        return segments.Count == 0 ? string.Empty : string.Join(':', segments) + ":";
    }

    private string ApplyPrefix(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return _keyPrefix;
        }

        return _keyPrefix + key;
    }

    public void Dispose()
    {
        _multiplexer?.Dispose();
        _database = null;
        _multiplexer = null;
    }
}
