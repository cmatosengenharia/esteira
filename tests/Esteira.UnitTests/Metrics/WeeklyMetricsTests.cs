using Esteira.Application.Metrics;
using Esteira.Domain.Entities;
using Esteira.Domain.Enums;

namespace Esteira.UnitTests.Metrics;

public class WeeklyMetricsTests
{
    // Quarta-feira; a semana atual começa na segunda 2026-11-02 00:00 UTC.
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static VideoJob JobAt(VideoStage target, DateTimeOffset at, double renderMinutes = 0)
    {
        var job = new VideoJob { OriginalFileName = "a.mp4", SourceObjectKey = "k" };
        job.Usage.RenderMinutes = renderMinutes;
        var stage = job.Stage;
        while (stage != target)
        {
            stage = VideoJob.NextStage(stage);
            job.BeginStage(stage, at);
            job.CompleteStage(stage, at);
        }
        return job;
    }

    [Fact]
    public void Empty_list_yields_four_zeroed_weeks()
    {
        var report = WeeklyMetrics.Compute([], Now);

        Assert.Equal("published_per_week", report.KeyMetric);
        Assert.Equal(0, report.Current);
        Assert.Equal(0, report.Trend);
        Assert.Equal(
            [new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 26), new DateOnly(2026, 11, 2)],
            report.Weeks.Select(w => w.WeekStart));
        Assert.All(report.Weeks, w => Assert.Equal((0, 0, 0d), (w.Completed, w.Published, w.RenderMinutes)));
    }

    [Fact]
    public void Counts_published_per_week_in_window_and_trend()
    {
        var jobs = new[]
        {
            JobAt(VideoStage.Published, new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero)), // fora da janela
            JobAt(VideoStage.Published, new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero)),
            JobAt(VideoStage.Published, new DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.Zero)),
            JobAt(VideoStage.Published, new DateTimeOffset(2026, 10, 30, 10, 0, 0, TimeSpan.Zero)),
            JobAt(VideoStage.Published, new DateTimeOffset(2026, 10, 31, 10, 0, 0, TimeSpan.Zero)),
            JobAt(VideoStage.Published, new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero)),
        };

        var report = WeeklyMetrics.Compute(jobs, Now);

        Assert.Equal([1, 0, 3, 1], report.Weeks.Select(w => w.Published));
        Assert.Equal(1, report.Current);
        Assert.Equal(-2, report.Trend);
    }

    [Fact]
    public void Job_published_at_monday_midnight_falls_in_new_week()
    {
        var report = WeeklyMetrics.Compute(
            [JobAt(VideoStage.Published, new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero))], Now);

        Assert.Equal([0, 0, 0, 1], report.Weeks.Select(w => w.Published));
        Assert.Equal(1, report.Trend);
    }

    [Fact]
    public void Failed_or_unpublished_jobs_do_not_count_as_published()
    {
        var at = new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);
        var failed = JobAt(VideoStage.Planned, at);
        failed.BeginStage(VideoStage.Rendered, at);
        failed.Fail("ffmpeg caiu", at);
        var awaiting = JobAt(VideoStage.AwaitingApproval, at);

        var report = WeeklyMetrics.Compute([failed, awaiting], Now);

        Assert.Equal(0, report.Current);
        Assert.Equal(1, report.Weeks[^1].Completed); // só o que renderizou de fato
    }

    [Fact]
    public void Sums_completed_and_render_minutes_per_week()
    {
        var jobs = new[]
        {
            JobAt(VideoStage.Rendered, new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero), 2.5),
            JobAt(VideoStage.CopyReady, new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero), 1.5),
            JobAt(VideoStage.Published, new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero), 3),
            JobAt(VideoStage.Planned, new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero)),
        };

        var report = WeeklyMetrics.Compute(jobs, Now);

        Assert.Equal([0, 1, 0, 2], report.Weeks.Select(w => w.Completed));
        Assert.Equal([0, 2.5, 0, 4.5], report.Weeks.Select(w => w.RenderMinutes));
    }
}
