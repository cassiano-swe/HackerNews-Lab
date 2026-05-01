using System.Text.Json;
using hacker.news.lab.application.contracts;
using hacker.news.lab.domain.models;
using hacker.news.lab.infrastructure.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace hacker.news.lab.infrastructure.Persistence;

public class RedisSnapshotStore : ISnapshotStore
{
    private readonly IDatabase _db;
    private readonly RedisOptions _options;

    public RedisSnapshotStore(
        IConnectionMultiplexer connection,
        IOptions<RedisOptions> options)
    {
        _db = connection.GetDatabase();
        _options = options.Value;
    }

    public async Task<IReadOnlyList<Story>> GetActiveSnapshotAsync(CancellationToken ct)
    {
        var pointer = await _db.StringGetAsync(_options.ActiveSnapshotPointerKey);

        if (pointer.IsNullOrEmpty)
            return [];

        var key = (RedisKey)pointer.ToString();

        var json = await _db.StringGetAsync(key);

        if (json.IsNullOrEmpty)
            return [];

        

        return JsonSerializer.Deserialize<IReadOnlyList<Story>>(json!) ?? [];
    }

    public async Task CreateTempSnapshotAsync(
        string tempSnapshotKey,
        IReadOnlyList<Story> stories,
        CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(stories);

        await _db.StringSetAsync(tempSnapshotKey, json);
    }

    public async Task SetActiveSnapshotAsync(
        string snapshotKey,
        CancellationToken ct)
    {
        await _db.StringSetAsync(
            _options.ActiveSnapshotPointerKey,
            snapshotKey);
    }
}