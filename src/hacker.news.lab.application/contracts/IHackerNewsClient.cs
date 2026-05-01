using hacker.news.lab.domain.models;

namespace hacker.news.lab.application.contracts;

public interface IHackerNewsClient
{
    Task<IReadOnlyList<long>> GetBestStoryIdsAsync(CancellationToken cancellationToken);
    Task<Story?> GetStoryAsync(long id, CancellationToken cancellationToken);
}