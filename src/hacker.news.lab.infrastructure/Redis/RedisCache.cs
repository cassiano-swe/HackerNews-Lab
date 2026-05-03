using System.Text.Json;
using StackExchange.Redis;
using hacker.news.lab.application.contracts;

namespace hacker.news.lab.infrastructure.Redis;

public sealed class RedisCache : ICache
{
    private readonly IDatabase _db;

    public RedisCache(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        var value = await _db.StringGetAsync(key);

        if (value.IsNullOrEmpty)
            return default;

        return JsonSerializer.Deserialize<T>(value!);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan ttl,
        CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(value);

        await _db.StringSetAsync(
            key,
            json,
            expiry: ttl);
    }
}