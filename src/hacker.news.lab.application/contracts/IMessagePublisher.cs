namespace hacker.news.lab.application.contracts;

public interface IMessagePublisher
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken);
    Task SubscribeAsync<T>(
        Func<T, CancellationToken, Task> handler,
        CancellationToken ct);
}