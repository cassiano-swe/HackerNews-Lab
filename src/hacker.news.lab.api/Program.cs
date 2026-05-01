
using hacker.news.lab.application.models;
using hacker.news.lab.domain.models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

app.MapGet("/api/v1/stories/best", (int? n) =>
{
    var take = n ?? 10;

    if (take <= 0 || take > 200)
        return Results.BadRequest("n must be between 1 and 200");

    // Fake snapshot
    var snapshot = GetFakeSnapshot();

    var result = snapshot
        .OrderByDescending(s => s.Score)
        .Take(take)
        .Select(s => new StoryResponse
        {
            Title = s.Title,
            Uri = s.Url,
            PostedBy = s.By,
            Time = s.Time,
            Score = s.Score,
            CommentCount = s.Descendants
        })
        .ToList();

    return Results.Ok(result);
})
    .WithName("GetBestStories")
    .WithTags("Stories")
    .WithSummary("Returns the top Hacker News stories ordered by score.")
    .WithDescription("The optional n query parameter controls the number of stories returned and must be between 1 and 200.")
    .Produces<List<StoryResponse>>(StatusCodes.Status200OK)
    .Produces<string>(StatusCodes.Status400BadRequest);

app.Run();

static List<Story> GetFakeSnapshot()
{
    return new List<Story>
    {
        new Story { Id = 1, Title = "Story A", Score = 100, By = "user1", Time = DateTime.UtcNow, Descendants = 10, Url = "https://a.com" },
        new Story { Id = 2, Title = "Story B", Score = 250, By = "user2", Time = DateTime.UtcNow, Descendants = 50, Url = "https://b.com" },
        new Story { Id = 3, Title = "Story C", Score = 180, By = "user3", Time = DateTime.UtcNow, Descendants = 30, Url = "https://c.com" }
    };
}
