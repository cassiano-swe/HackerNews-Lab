namespace hacker.news.lab.domain.models;

public class Story
{
    public long Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Url { get; set; }
    public string By { get; set; } = default!;
    public DateTime Time { get; set; }
    public int Score { get; set; }
    public int Descendants { get; set; }
}