using hacker.news.lab.application.contracts;
using hacker.news.lab.infrastructure.Clients.HackerNews;
using hacker.news.lab.infrastructure.Persistence;
using hacker.news.lab.infrastructure.Messaging;
using hacker.news.lab.infrastructure.Redis;
using hacker.news.lab.worker;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));

builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect("redis:6379"));

builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
builder.Services.AddSingleton<ISnapshotStore, RedisSnapshotStore>();
builder.Services.AddSingleton<ICache, RedisCache>();

builder.Services.AddHttpClient<IHackerNewsClient, HackerNewsClient>(client =>
{
    client.BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/");
});

builder.Services.AddHostedService<BestStoriesWorker>();

var app = builder.Build();

app.Run();