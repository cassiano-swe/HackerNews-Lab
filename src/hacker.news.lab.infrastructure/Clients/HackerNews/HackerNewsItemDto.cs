using System.Text.Json.Serialization;

namespace hacker.news.lab.infrastructure.Clients.HackerNews;

public class HackerNewsItemDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("by")]
    public string? By { get; set; }

    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("descendants")]
    public int Descendants { get; set; }
}