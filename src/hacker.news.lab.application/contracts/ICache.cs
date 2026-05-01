namespace hacker.news.lab.application.contracts;

public interface ICache
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken);
    Task SetAsync<T>(string key, T value, TimeSpan? ttl, CancellationToken cancellationToken);
}