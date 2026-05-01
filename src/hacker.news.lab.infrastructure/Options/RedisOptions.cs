namespace hacker.news.lab.infrastructure.Options;

public class RedisOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";

    public string ActiveSnapshotPointerKey { get; set; } 
        = "hn:stories:snapshot:active";

    public string SnapshotKeyPrefix { get; set; } 
        = "hn:stories:snapshot";
}