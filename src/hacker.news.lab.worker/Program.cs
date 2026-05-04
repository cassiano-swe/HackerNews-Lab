using hacker.news.lab.application.contracts;
using hacker.news.lab.infrastructure.Clients.HackerNews;
using hacker.news.lab.infrastructure.Persistence;
using hacker.news.lab.infrastructure.Messaging;
using hacker.news.lab.infrastructure.Redis;
using hacker.news.lab.infrastructure.resilience;
using hacker.news.lab.worker;
using StackExchange.Redis;
using Polly;
using Polly.Extensions.Http;
using System.Net;

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

builder.Services
    .AddHttpClient<IHackerNewsClient, HackerNewsClient>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

builder.Services.AddHostedService<BestStoriesWorker>();

var app = builder.Build();

app.Run();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30));
}