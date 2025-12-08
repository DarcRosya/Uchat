using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Uchat.Server.Services.Redis;

public interface IRedisService : IDisposable
{
    bool IsAvailable { get; }

    Task SetHashAsync(string key, string field, string value, TimeSpan? ttl = null);
    Task<string?> GetHashAsync(string key, string field);
    Task<Dictionary<string, string?>> GetHashAsync(string key, IEnumerable<string> fields);
    Task UpdateSortedSetAsync(string key, string member, double score, TimeSpan? ttl = null);
    Task<IEnumerable<string>> GetSortedSetMembersAsync(string key, long start = 0, long stop = -1, Order order = Order.Descending);
    Task<List<string>> GetSortedKeysAsync(string sortedSetKey);
    Task<bool> HashDeleteAsync(string key, string field);
    Task<long> PublishAsync(string channel, string message);
    Task<bool> KeyDeleteAsync(string key);
}
