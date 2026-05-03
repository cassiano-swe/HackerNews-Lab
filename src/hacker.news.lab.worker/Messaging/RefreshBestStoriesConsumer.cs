using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using hacker.news.lab.domain.Events;

namespace hacker.news.lab.worker.Messaging;

public class RefreshBestStoriesConsumer : BackgroundService
{
    private readonly ILogger<RefreshBestStoriesConsumer> _logger;

    public RefreshBestStoriesConsumer(ILogger<RefreshBestStoriesConsumer> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory { HostName = "rabbitmq" };

        IConnection? connection = null;
        IModel? channel = null;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                connection = factory.CreateConnection();
                channel = connection.CreateModel();

                channel.QueueDeclare(
                    queue: "refresh-best-stories",
                    durable: true,
                    exclusive: false,
                    autoDelete: false);

                break; // conectou com sucesso
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ not ready, retrying in 5s...");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }

        var consumer = new EventingBasicConsumer(channel);

        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);

            var message = JsonSerializer.Deserialize<RefreshBestStoriesRequested>(json);

            _logger.LogInformation("Received event at {Time}", message?.RequestedAt);

            channel!.BasicAck(ea.DeliveryTag, false);
        };

        channel.BasicConsume(
            queue: "refresh-best-stories",
            autoAck: false,
            consumer: consumer);
    }
}