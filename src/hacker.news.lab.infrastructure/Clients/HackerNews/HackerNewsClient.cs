using System.Net.Http.Json;
using hacker.news.lab.application.contracts;
using hacker.news.lab.domain;
using hacker.news.lab.domain.models;

namespace hacker.news.lab.infrastructure.Clients.HackerNews;

public class HackerNewsClient : IHackerNewsClient
{
    private readonly HttpClient _httpClient;

    public HackerNewsClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<long>> GetBestStoryIdsAsync(CancellationToken ct)
    {
        var result = await _httpClient.GetFromJsonAsync<List<long>>(
            "beststories.json",
            cancellationToken: ct);

        return result ?? [];
    }

    public async Task<Story?> GetStoryByIdAsync(long id, CancellationToken ct)
    {
        var dto = await _httpClient.GetFromJsonAsync<HackerNewsItemDto>(
            $"item/{id}.json",
            cancellationToken: ct);

        if (dto == null || dto.Title == null)
            return null;

        return Map(dto);
    }

    private static Story Map(HackerNewsItemDto dto)
    {
        return new Story()
        {
            Title = dto.Title!,
            Uri = dto.Url,
            By = dto.By ?? string.Empty,
            Time = DateTimeOffset.FromUnixTimeSeconds(dto.Time).DateTime,
            Score = dto.Score,
            Descendants = dto.Descendants
        };
    }
}