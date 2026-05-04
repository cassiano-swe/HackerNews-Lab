using hacker.news.lab.application.contracts;
using hacker.news.lab.infrastructure.Clients.HackerNews;
using hacker.news.lab.infrastructure.Persistence;
using hacker.news.lab.infrastructure.Messaging;
using hacker.news.lab.infrastructure.Redis;
using hacker.news.lab.worker;
using StackExchange.Redis;
using Polly;
using Polly.Extensions.Http;
using System.Net;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

var builder = Host.CreateApplicationBuilder(args);

// =========================
// CONFIG
// =========================

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));

// =========================
// REDIS
// =========================

builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect("redis:6379"));

builder.Services.AddSingleton<ISnapshotStore, RedisSnapshotStore>();
builder.Services.AddSingleton<ICache, RedisCache>();

// =========================
// MESSAGING
// =========================

builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();

// =========================
// HTTP CLIENT + RESILIÊNCIA
// =========================

builder.Services
    .AddHttpClient<IHackerNewsClient, HackerNewsClient>(client =>
    {
        client.BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/");
    })
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

// =========================
// WORKER
// =========================

builder.Services.AddHostedService<BestStoriesWorker>();

// =========================
// OBSERVABILIDADE
// =========================

var serviceName = "hacker.news.lab.worker";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName))
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("hacker.news.lab.worker")
            .AddRuntimeInstrumentation()
            .AddHttpClientInstrumentation()
            .AddPrometheusHttpListener(options =>
            {
                options.UriPrefixes = new[] { "http://+:8080/" };
            });
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(serviceName)
            .AddSource("hacker.news.lab.messaging")
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri("http://jaeger:4317");
            });
    });

// =========================
// RUN
// =========================

var app = builder.Build();

app.Run();

// =========================
// POLLY
// =========================

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