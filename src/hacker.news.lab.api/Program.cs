using hacker.news.lab.api.Jobs;
using hacker.news.lab.application.contracts;
using hacker.news.lab.application.models;
using hacker.news.lab.domain.events;
using hacker.news.lab.infrastructure;
using hacker.news.lab.infrastructure.Clients.HackerNews;
using hacker.news.lab.infrastructure.Exceptions;
using Hangfire;
using Hangfire.Dashboard;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

const string serviceName = "hacker.news.lab.api";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpClient<IHackerNewsClient, HackerNewsClient>(client =>
{
    client.BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/");
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => { resource.AddService(serviceName); })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource(serviceName)
            .AddSource("hacker.news.lab.messaging")
            .AddOtlpExporter(options => { options.Endpoint = new Uri("http://jaeger:4317"); });
    });

var app = builder.Build();

app.Use(async (context, next) =>
{
    var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString();

    context.Response.Headers["X-Trace-Id"] = traceId ?? string.Empty;

    await next();
});

app.UseExceptionHandler();

var hangfireDashboardOptions = new DashboardOptions();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    hangfireDashboardOptions.Authorization = [];
}

app.UseHangfireDashboard("/hacker-news-lab/hangfire", hangfireDashboardOptions);

app.MapPrometheusScrapingEndpoint("/metrics");

// Endpoints
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
        {
            return Results.BadRequest("Invalid 'n'");
        }

        var stories = await snapshotStore.GetActiveSnapshotAsync(ct);

        var result = stories
            .OrderByDescending(story => story.Score)
            .Take(n)
            .Select(static story => new
            {
                title = story.Title,
                uri = story.Uri,
                by = story.By,
                time = story.Time,
                score = story.Score,
                commentCount = story.Descendants
            });

        return Results.Ok(result);
    })
    .WithName("GetBestStories")
    .WithTags("Stories")
    .WithSummary("Returns the top Hacker News stories ordered by score.")
    .WithDescription("The n query parameter controls the number of stories returned and must be between 1 and 200.")
    .Produces<List<StoryResponse>>()
    .Produces<string>(StatusCodes.Status400BadRequest);

app.MapPost("/api/v1/stories/refresh", async (
        IMessagePublisher publisher,
        CancellationToken ct) =>
    {
        await publisher.PublishAsync(
            new RefreshBestStoriesRequested(DateTime.UtcNow),
            ct);

        return Results.Accepted();
    })
    .WithName("RefreshBestStories")
    .WithTags("Stories")
    .WithSummary("Publishes a request to refresh the Hacker News best stories cache.")
    .Produces(StatusCodes.Status202Accepted);

// Jobs
app.Services
    .GetRequiredService<IBackgroundJobClient>()
    .Enqueue<RefreshBestStoriesJob>(job => job.PublishAndScheduleNextAsync());


app.Run();
