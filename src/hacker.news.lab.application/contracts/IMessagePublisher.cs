namespace hacker.news.lab.application.contracts;

public interface IMessagePublisher
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken);
}