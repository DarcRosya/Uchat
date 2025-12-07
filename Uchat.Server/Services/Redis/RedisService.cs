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
    private ConnectionMultiplexer? _multiplexer;
    private IDatabase? _database;

    public bool IsAvailable => _database != null && _multiplexer?.IsConnected == true;

    public RedisService(IOptions<RedisSettings> options, ILogger<RedisService> logger)
    {
        _logger = logger;
        _settings = options.Value;
        _keyPrefix = BuildKeyPrefix(_settings.InstanceName, _settings.ChannelPrefix);
        _defaultTtl = _settings.DefaultEntryTtlSeconds > 0
            ? TimeSpan.FromSeconds(_settings.DefaultEntryTtlSeconds)
            : null;

        if (string.IsNullOrWhiteSpace(_settings.ConnectionString))
        {
            _logger.LogInformation("Redis connection string is not configured; caching/presence disabled.");
            return;
        }

        try
        {
            _multiplexer = ConnectionMultiplexer.Connect(_settings.ConnectionString);
            _database = _multiplexer.GetDatabase();
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
