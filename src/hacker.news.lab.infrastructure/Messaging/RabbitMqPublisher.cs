using System.Text;
using System.Text.Json;
using hacker.news.lab.application.contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace hacker.news.lab.infrastructure.Messaging;

public sealed class RabbitMqPublisher : IMessagePublisher, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            DispatchConsumersAsync = true
        };

        _connection = CreateConnectionWithRetry(factory, _logger);
        _channel = _connection.CreateModel();

        DeclareQueue();
    }

    public Task PublishAsync<T>(T message, CancellationToken ct = default)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        _channel.BasicPublish(
            exchange: "",
            routingKey: _options.Queue,
            basicProperties: null,
            body: body);

        return Task.CompletedTask;
    }

    public Task SubscribeAsync<T>(
        Func<T, CancellationToken, Task> handler,
        CancellationToken ct = default)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.Received += async (_, args) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(args.Body.ToArray());

                var message = JsonSerializer.Deserialize<T>(json);

                if (message is null)
                {
                    _channel.BasicNack(
                        deliveryTag: args.DeliveryTag,
                        multiple: false,
                        requeue: false);

                    return;
                }

                await handler(message, ct);

                _channel.BasicAck(
                    deliveryTag: args.DeliveryTag,
                    multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing RabbitMQ message");

                _channel.BasicNack(
                    deliveryTag: args.DeliveryTag,
                    multiple: false,
                    requeue: true);
            }
        };

        _channel.BasicConsume(
            queue: _options.Queue,
            autoAck: false,
            consumer: consumer);

        return Task.CompletedTask;
    }

    private void DeclareQueue()
    {
        _channel.QueueDeclare(
            queue: _options.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false);
    }

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }

    private static IConnection CreateConnectionWithRetry(
    ConnectionFactory factory,
    ILogger<RabbitMqPublisher> logger)
    {
        const int maxAttempts = 10;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return factory.CreateConnection();
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(
                    ex,
                    "RabbitMQ unavailable. Retrying connection attempt {Attempt}/{MaxAttempts}",
                    attempt,
                    maxAttempts);

                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }

        return factory.CreateConnection();
    }
}