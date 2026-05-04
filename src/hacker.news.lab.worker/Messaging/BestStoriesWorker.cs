using hacker.news.lab.application.contracts;
using hacker.news.lab.domain.events;
using hacker.news.lab.domain.models;
using System.Diagnostics;

namespace hacker.news.lab.worker;

public sealed class BestStoriesWorker : BackgroundService
{
    private readonly IMessagePublisher _bus;
    private readonly IHackerNewsClient _client;
    private readonly ISnapshotStore _snapshot;
    private readonly ICache _cache;
    private readonly ILogger<BestStoriesWorker> _logger;
    private static readonly ActivitySource ActivitySource =
    new("hacker.news.lab.worker");

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
        using var activity = ActivitySource.StartActivity("process-best-stories");

        activity?.SetTag("worker", "best-stories");

        return _bus.SubscribeAsync<RefreshBestStoriesRequested>(Handle, stoppingToken);
    }

    private async Task Handle(RefreshBestStoriesRequested _, CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var cancellationToken = linkedCts.Token;

        _logger.LogInformation("Processing best stories...");

        try
        {
            var ids = await _client.GetBestStoryIdsAsync(cancellationToken);

            var topIds = ids
                .Take(200)
                .ToList();

            using var semaphore = new SemaphoreSlim(10);

            var tasks = topIds.Select(async id =>
            {
                await semaphore.WaitAsync(cancellationToken);

                try
                {
                    var cacheKey = $"hn:story:{id}";

                    var cached = await _cache.GetAsync<Story>(cacheKey, cancellationToken);
                    if (cached is not null)
                        return cached;

                    var story = await _client.GetStoryByIdAsync(id, cancellationToken);
                    if (story is null)
                        return null;

                    await _cache.SetAsync(
                        cacheKey,
                        story,
                        TimeSpan.FromMinutes(10),
                        cancellationToken);

                    return story;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch story {StoryId}", id);
                    return null;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var stories = await Task.WhenAll(tasks);

            var ordered = stories
                .Where(x => x is not null)
                .Select(x => x!)
                .OrderByDescending(x => x.Score)
                .ToList();

            if (!ordered.Any())
            {
                _logger.LogWarning("Snapshot vazio - mantendo snapshot anterior");
                return;
            }

            if (ordered.Count < 10)
            {
                _logger.LogWarning(
                    "Snapshot inválido. Apenas {Count} stories válidas. Mantendo snapshot anterior",
                    ordered.Count);

                return;
            }

            var tempKey = $"hn:stories:snapshot:temp:{Guid.NewGuid()}";

            await _snapshot.SetSnapshotAsync(tempKey, ordered, cancellationToken);

            await _snapshot.SetActiveSnapshotAsync(tempKey, cancellationToken);

            Metrics.StoriesProcessed.Add(ordered.Count);

            _logger.LogInformation(
                "Snapshot atualizado com sucesso com {Count} stories",
                ordered.Count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Worker cancelado pela aplicação");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Timeout ao processar best stories. Mantendo snapshot anterior");
        }
        catch (Exception ex)
        {
            Metrics.Errors.Add(1);
            _logger.LogError(ex, "Erro ao processar best stories. Mantendo snapshot anterior");
        }
    }
}
