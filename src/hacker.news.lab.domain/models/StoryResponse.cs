namespace hacker.news.lab.application.models;

public class StoryResponse
{
    public string Title { get; set; } = default!;
    public string? Uri { get; set; }
    public string PostedBy { get; set; } = default!;
    public DateTime Time { get; set; }
    public int Score { get; set; }
    public int CommentCount { get; set; }
}