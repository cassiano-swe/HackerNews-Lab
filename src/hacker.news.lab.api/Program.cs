using hacker.news.lab.application.contracts;
using hacker.news.lab.application.models;
using hacker.news.lab.domain.events;
using hacker.news.lab.infrastructure;
using hacker.news.lab.infrastructure.Exceptions;
using hacker.news.lab.infrastructure.Clients.HackerNews;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
const string serviceName = "hacker.news.lab.api";

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpClient<IHackerNewsClient, HackerNewsClient>(client =>
{
    client.BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/");
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource(serviceName)
            .AddSource("hacker.news.lab.messaging")
            .AddOtlpExporter(o => { o.Endpoint = new Uri("http://jaeger:4317"); });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();
    });

var app = builder.Build();

app.Use(async (context, next) =>
{
    var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString();
    context.Response.Headers["X-Trace-Id"] = traceId ?? "";
    await next();
});

app.UseExceptionHandler();

app.MapPrometheusScrapingEndpoint("/metrics");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .WithSummary("Returns the API health status.")
    .Produces(StatusCodes.Status200OK);

app.MapGet("/api/v1/stories/best", async (
        int n,
        ISnapshotStore snapshotStore,
        CancellationToken ct) =>
    {
        if (n <= 0 || n > 200)
            return Results.BadRequest("Invalid 'n'");

        var stories = await snapshotStore.GetActiveSnapshotAsync(ct);

        var result = stories
            .OrderByDescending(x => x.Score)
            .Take(n)
            .Select(static x => new
            {
                title = x.Title,
                uri = x.Uri,
                by = x.By,
                time = x.Time,
                score = x.Score,
                commentCount = x.Descendants
            });

        return Results.Ok(result);
    })
    .WithName("GetBestStories")
    .WithTags("Stories")
    .WithSummary("Returns the top Hacker News stories ordered by score.")
    .WithDescription(
        "The optional n query parameter controls the number of stories returned and must be between 1 and 200.")
    .Produces<List<StoryResponse>>()
    .Produces<string>(StatusCodes.Status400BadRequest);

app.MapPost("/api/v1/stories/refresh", async (IMessagePublisher publisher, CancellationToken ct) =>
{
    await publisher.PublishAsync(
        new RefreshBestStoriesRequested(DateTime.UtcNow),
        ct);

    return Results.Accepted();
});

app.Run();
