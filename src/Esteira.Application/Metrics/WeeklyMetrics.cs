using Esteira.Domain.Entities;
using Esteira.Domain.Enums;

namespace Esteira.Application.Metrics;

public sealed record WeekBucket(DateOnly WeekStart, int Completed, int Published, double RenderMinutes);

public sealed record WeeklyMetricsReport(string KeyMetric, int Current, int Trend, IReadOnlyList<WeekBucket> Weeks);

/// <summary>
/// Métrica-chave única do estágio (lean analytics): vídeos publicados por semana, com tendência contra a semana anterior.
/// Semanas de segunda 00:00 UTC à segunda seguinte; a última da lista é a atual.
/// </summary>
public static class WeeklyMetrics
{
    public const string KeyMetric = "published_per_week";

    public static DateTimeOffset WeekStart(DateTimeOffset at)
    {
        var utc = at.ToUniversalTime();
        var daysSinceMonday = ((int)utc.DayOfWeek + 6) % 7;
        return new DateTimeOffset(utc.Date.AddDays(-daysSinceMonday), TimeSpan.Zero);
    }

    public static WeeklyMetricsReport Compute(IEnumerable<VideoJob> jobs, DateTimeOffset now, int weeks = 4)
    {
        var first = WeekStart(now).AddDays(-7 * (weeks - 1));
        var completed = new int[weeks];
        var published = new int[weeks];
        var renderMinutes = new double[weeks];

        foreach (var job in jobs.Where(j => j.Stage != VideoStage.Failed))
        {
            if (Index(FinishedAt(job, VideoStage.Rendered)) is { } r)
            {
                completed[r]++;
                renderMinutes[r] += job.Usage.RenderMinutes;
            }
            if (Index(FinishedAt(job, VideoStage.Published)) is { } p)
                published[p]++;
        }

        var buckets = Enumerable.Range(0, weeks)
            .Select(i => new WeekBucket(DateOnly.FromDateTime(first.AddDays(7 * i).UtcDateTime), completed[i], published[i], renderMinutes[i]))
            .ToList();
        var current = published[^1];
        var previous = weeks > 1 ? published[^2] : 0;
        return new WeeklyMetricsReport(KeyMetric, current, current - previous, buckets);

        int? Index(DateTimeOffset? at)
        {
            if (at is null) return null;
            var i = (int)Math.Floor((at.Value - first).TotalDays / 7);
            return i >= 0 && i < weeks ? i : null;
        }
    }

    private static DateTimeOffset? FinishedAt(VideoJob job, VideoStage stage) =>
        job.Timings.LastOrDefault(t => t.Stage == stage && t.FinishedAt is not null)?.FinishedAt;
}
