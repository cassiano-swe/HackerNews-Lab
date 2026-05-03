using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using hacker.news.lab.application.contracts;
using Microsoft.Extensions.Options;

namespace hacker.news.lab.infrastructure.Messaging;

public class RabbitMqPublisher : IMessagePublisher
{
    private readonly RabbitMqOptions _options;
    private readonly IConnection _connection;

    public RabbitMqPublisher(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;

        var factory = new ConnectionFactory
        {
            HostName = _options.Host
        };

        _connection = factory.CreateConnection();
    }

    public Task PublishAsync<T>(T message, CancellationToken ct = default)
    {
        using var channel = _connection.CreateModel();

        channel.QueueDeclare(
            queue: _options.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        channel.BasicPublish(
            exchange: "",
            routingKey: _options.Queue,
            basicProperties: null,
            body: body);

        return Task.CompletedTask;
    }
}