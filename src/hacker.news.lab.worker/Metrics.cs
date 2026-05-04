using System.Diagnostics.Metrics;

public static class Metrics
{
    public static readonly Meter Meter = new("hacker.news.lab.worker");

    public static readonly Counter<int> StoriesProcessed =
        Meter.CreateCounter<int>("stories_processed");

    public static readonly Counter<int> Errors =
        Meter.CreateCounter<int>("worker_errors");
}