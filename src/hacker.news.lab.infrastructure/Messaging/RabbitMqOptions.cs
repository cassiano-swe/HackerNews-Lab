namespace hacker.news.lab.infrastructure.Messaging;

public class RabbitMqOptions
{
    public string Host { get; set; } = "rabbitmq";
    public string Queue { get; set; } = "refresh-best-stories";
}