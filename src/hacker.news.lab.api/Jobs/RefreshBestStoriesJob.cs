using hacker.news.lab.application.contracts;
using hacker.news.lab.domain.events;
using Hangfire;

namespace hacker.news.lab.api.Jobs;

public sealed class RefreshBestStoriesJob(
    IMessagePublisher publisher,
    IBackgroundJobClient backgroundJobs,
    ILogger<RefreshBestStoriesJob> logger)
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    public async Task PublishAndScheduleNextAsync()
    {
        await publisher.PublishAsync(
            new RefreshBestStoriesRequested(DateTime.UtcNow),
            CancellationToken.None);

        logger.LogInformation(
            "Refresh best stories event published. Next execution scheduled in {Seconds} seconds.",
            RefreshInterval.TotalSeconds);

        backgroundJobs.Schedule<RefreshBestStoriesJob>(
            job => job.PublishAndScheduleNextAsync(),
            RefreshInterval);
    }
}
