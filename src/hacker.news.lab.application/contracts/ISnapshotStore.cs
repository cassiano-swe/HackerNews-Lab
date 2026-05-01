using hacker.news.lab.domain.models;

namespace hacker.news.lab.application.contracts;

public interface ISnapshotStore
{
    Task<IReadOnlyList<Story>?> GetActiveSnapshotAsync(CancellationToken cancellationToken);

    Task CreateTempSnapshotAsync(string key, IReadOnlyList<Story> stories, CancellationToken cancellationToken);

    Task SetActiveSnapshotAsync(string key, CancellationToken cancellationToken);
}