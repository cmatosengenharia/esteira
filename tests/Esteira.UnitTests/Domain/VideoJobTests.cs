using Esteira.Domain.Entities;
using Esteira.Domain.Enums;
using Esteira.Domain.Exceptions;

namespace Esteira.UnitTests.Domain;

public class VideoJobTests
{
    private static VideoJob NewJob() => new() { OriginalFileName = "a.mp4", SourceObjectKey = "s/a.mp4" };

    [Fact]
    public void Stages_advance_in_order_and_record_timing()
    {
        var job = NewJob();
        var t0 = DateTimeOffset.UtcNow;
        job.BeginStage(VideoStage.Ingested, t0);
        job.CompleteStage(VideoStage.Ingested, t0.AddMilliseconds(250));
        Assert.Equal(VideoStage.Ingested, job.Stage);
        Assert.Equal(250, job.Timings.Single().DurationMs);
        Assert.True(job.AlreadyPast(VideoStage.Ingested));
        Assert.False(job.AlreadyPast(VideoStage.Transcribed));
    }

    [Fact]
    public void Skipping_a_stage_is_rejected()
    {
        var job = NewJob();
        Assert.Throws<DomainException>(() => job.BeginStage(VideoStage.Transcribed, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Budget_is_enforced()
    {
        var job = NewJob();
        job.Budget.MaxRenderMinutes = 1;
        job.Usage.RenderMinutes = 0.8;
        var ex = Assert.Throws<BudgetExceededException>(() => job.EnsureRenderBudget(0.5));
        Assert.Equal("render minutes", ex.Resource);
        job.EnsureRenderBudget(0.2);
    }

    [Fact]
    public void Fail_closes_open_timings()
    {
        var job = NewJob();
        var t0 = DateTimeOffset.UtcNow;
        job.BeginStage(VideoStage.Ingested, t0);
        job.Fail("ffprobe exit 1", t0.AddSeconds(1));
        Assert.Equal(VideoStage.Failed, job.Stage);
        Assert.Equal("ffprobe exit 1", job.FailureReason);
        Assert.NotNull(job.Timings.Single().FinishedAt);
    }
}
