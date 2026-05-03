using hacker.news.lab.application.contracts;
using hacker.news.lab.domain.models;
using hacker.news.lab.domain.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace hacker.news.lab.worker;

public sealed class BestStoriesWorker : BackgroundService
{
    private readonly IMessagePublisher _bus;
    private readonly IHackerNewsClient _client;
    private readonly ISnapshotStore _snapshot;
    private readonly ICache _cache;
    private readonly ILogger<BestStoriesWorker> _logger;

    public BestStoriesWorker(
        IMessagePublisher bus,
        IHackerNewsClient client,
        ISnapshotStore snapshot,
        ICache cache,
        ILogger<BestStoriesWorker> logger)
    {
        _bus = bus;
        _client = client;
        _snapshot = snapshot;
        _cache = cache;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _bus.SubscribeAsync<RefreshBestStoriesRequested>(Handle, stoppingToken);
    }

    private async Task Handle(RefreshBestStoriesRequested _, CancellationToken ct)
    {
        _logger.LogInformation("Processing best stories...");

        // 1. Buscar IDs
        var ids = await _client.GetBestStoryIdsAsync(ct);

        // Limite defensivo (ex: top 200)
        var topIds = ids.Take(200).ToList();

        var stories = new List<Story>();

        // 2. Buscar detalhes (cache + API)
        foreach (var id in topIds)
        {
            var cacheKey = $"hn:story:{id}";

            var cached = await _cache.GetAsync<Story>(cacheKey, ct);
            if (cached is not null)
            {
                stories.Add(cached);
                continue;
            }

            var story = await _client.GetStoryByIdAsync(id, ct);
            if (story is null) continue;

            await _cache.SetAsync(cacheKey, story, TimeSpan.FromMinutes(10), ct);

            stories.Add(story);
        }

        // 3. Ordenar
        var ordered = stories
            .OrderByDescending(x => x.Score)
            .ToList();

        // 4. Snapshot temporário
        var tempKey = $"hn:stories:snapshot:temp:{Guid.NewGuid()}";

        await _snapshot.SetSnapshotAsync(tempKey, ordered, ct);

        // 5. Validação simples
        if (!ordered.Any())
        {
            _logger.LogWarning("Snapshot vazio - abortando swap");
            return;
        }

        // 6. Atomic swap
        await _snapshot.SetActiveSnapshotAsync(tempKey, ct);

        _logger.LogInformation("Snapshot atualizado com sucesso");
    }
}