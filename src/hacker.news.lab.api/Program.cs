
using hacker.news.lab.infrastructure;
using hacker.news.lab.application.models;
using hacker.news.lab.application.contracts;
using hacker.news.lab.infrastructure.Clients.HackerNews;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpClient<IHackerNewsClient, HackerNewsClient>(client =>
{
    client.BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/");
});

var app = builder.Build();

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
        .Select(x => new
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
    .WithDescription("The optional n query parameter controls the number of stories returned and must be between 1 and 200.")
    .Produces<List<StoryResponse>>(StatusCodes.Status200OK)
    .Produces<string>(StatusCodes.Status400BadRequest);

app.Run();