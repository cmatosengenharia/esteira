using Esteira.Application.Abstractions;
using Esteira.Application.Messaging;
using Esteira.Domain.Entities;
using Esteira.Domain.Enums;
using Esteira.Domain.Exceptions;
using Esteira.Infrastructure.Persistence;
using Esteira.Infrastructure.Tenancy;
using Esteira.Workers.Consumers;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Esteira.UnitTests.Workers;

/// <summary>Prova a semântica do StageConsumer com o harness em memória do MassTransit e SQLite.</summary>
public sealed class StageConsumerTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _provider;
    private readonly ITestHarness _harness;
    private readonly Guid _tenantId = Guid.NewGuid();

    public static class Behaviour
    {
        public static Func<VideoJob, Exception?> Fail = _ => null;
        public static int Executions;
    }

    private sealed class FakeIngestConsumer(IEsteiraDbContext db, ITenantContextSetter setter, IClock clock, ILogger<FakeIngestConsumer> logger)
        : StageConsumer<IngestRequested>(db, setter, clock, logger)
    {
        protected override VideoStage TargetStage => VideoStage.Ingested;
        protected override void EnsureBudget(VideoJob job) => job.EnsureRenderBudget(0);
        protected override Task<object?> ExecuteAsync(VideoJob job, CancellationToken ct)
        {
            Behaviour.Executions++;
            var ex = Behaviour.Fail(job);
            if (ex is not null) throw ex;
            job.DurationSeconds = 20;
            return Task.FromResult<object?>(new TranscribeRequested(job.Id, job.TenantId));
        }
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

    public StageConsumerTests()
    {
        _connection.Open();
        var services = new ServiceCollection();
        services.AddLogging(b => b.ClearProviders().AddProvider(NullLoggerProvider.Instance));
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());
        services.AddSingleton<IClock, FixedClock>();
        services.AddDbContext<EsteiraDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<IEsteiraDbContext>(sp => sp.GetRequiredService<EsteiraDbContext>());
        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<FakeIngestConsumer>();
            x.UsingInMemory((ctx, cfg) =>
            {
                cfg.ReceiveEndpoint(Queues.Ingest, e =>
                {
                    e.UseMessageRetry(r => r.Immediate(Infrastructure.Messaging.MessagingExtensions.MaxRetries));
                    e.ConfigureConsumer<FakeIngestConsumer>(ctx);
                });
            });
        });
        _provider = services.BuildServiceProvider(true);
        using (var scope = _provider.CreateScope())
            scope.ServiceProvider.GetRequiredService<EsteiraDbContext>().Database.EnsureCreated();
        _harness = _provider.GetRequiredService<ITestHarness>();
        _harness.TestTimeout = TimeSpan.FromSeconds(10);
        Behaviour.Fail = _ => null;
        Behaviour.Executions = 0;
    }

    private async Task<VideoJob> SeedJobAsync()
    {
        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(_tenantId);
        var db = scope.ServiceProvider.GetRequiredService<EsteiraDbContext>();
        var job = new VideoJob { OriginalFileName = "a.mp4", SourceObjectKey = "s/a.mp4" };
        db.VideoJobs.Add(job);
        await db.SaveChangesAsync(CancellationToken.None);
        return job;
    }

    private async Task<VideoJob> ReloadAsync(Guid id)
    {
        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(_tenantId);
        return await scope.ServiceProvider.GetRequiredService<EsteiraDbContext>().VideoJobs.SingleAsync(j => j.Id == id);
    }

    [Fact]
    public async Task Happy_path_advances_stage_records_timing_and_publishes_next()
    {
        await _harness.Start();
        var job = await SeedJobAsync();
        await _harness.Bus.Publish(new IngestRequested(job.Id, _tenantId));

        Assert.True(await _harness.Consumed.Any<IngestRequested>());
        Assert.True(await _harness.Published.Any<TranscribeRequested>());

        var reloaded = await ReloadAsync(job.Id);
        Assert.Equal(VideoStage.Ingested, reloaded.Stage);
        Assert.Equal(20, reloaded.DurationSeconds);
        var timing = Assert.Single(reloaded.Timings);
        Assert.NotNull(timing.FinishedAt);
    }

    [Fact]
    public async Task Duplicate_message_is_ignored()
    {
        await _harness.Start();
        var job = await SeedJobAsync();
        await _harness.Bus.Publish(new IngestRequested(job.Id, _tenantId));
        Assert.True(await _harness.Consumed.Any<IngestRequested>());
        await _harness.Bus.Publish(new IngestRequested(job.Id, _tenantId));
        await _harness.InactivityTask;

        Assert.Equal(1, Behaviour.Executions);
        Assert.Single((await ReloadAsync(job.Id)).Timings);
    }

    [Fact]
    public async Task Message_for_another_tenant_cannot_see_the_job()
    {
        await _harness.Start();
        var job = await SeedJobAsync();
        await _harness.Bus.Publish(new IngestRequested(job.Id, Guid.NewGuid()));
        Assert.True(await _harness.Consumed.Any<IngestRequested>());
        await _harness.InactivityTask;

        Assert.Equal(0, Behaviour.Executions);
        Assert.Equal(VideoStage.Uploaded, (await ReloadAsync(job.Id)).Stage);
    }

    [Fact]
    public async Task Budget_exceeded_fails_job_without_retry()
    {
        await _harness.Start();
        var job = await SeedJobAsync();
        using (var scope = _provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(_tenantId);
            var db = scope.ServiceProvider.GetRequiredService<EsteiraDbContext>();
            var j = await db.VideoJobs.SingleAsync(x => x.Id == job.Id);
            j.Usage.RenderMinutes = j.Budget.MaxRenderMinutes + 1;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        await _harness.Bus.Publish(new IngestRequested(job.Id, _tenantId));
        Assert.True(await _harness.Consumed.Any<IngestRequested>());
        await _harness.InactivityTask;

        var reloaded = await ReloadAsync(job.Id);
        Assert.Equal(VideoStage.Failed, reloaded.Stage);
        Assert.StartsWith("BudgetExceeded", reloaded.FailureReason);
        Assert.Equal(0, Behaviour.Executions);
        Assert.False(await _harness.Published.Any<TranscribeRequested>());
    }

    [Fact]
    public async Task Domain_error_fails_job_without_retry()
    {
        Behaviour.Fail = _ => new DomainException("sem áudio");
        await _harness.Start();
        var job = await SeedJobAsync();
        await _harness.Bus.Publish(new IngestRequested(job.Id, _tenantId));
        Assert.True(await _harness.Consumed.Any<IngestRequested>());
        await _harness.InactivityTask;

        var reloaded = await ReloadAsync(job.Id);
        Assert.Equal(VideoStage.Failed, reloaded.Stage);
        Assert.Equal("sem áudio", reloaded.FailureReason);
        Assert.Equal(1, Behaviour.Executions);
    }

    [Fact]
    public async Task Infrastructure_error_retries_then_fails_and_faults()
    {
        Behaviour.Fail = _ => new IOException("ffmpeg caiu");
        await _harness.Start();
        var job = await SeedJobAsync();
        await _harness.Bus.Publish(new IngestRequested(job.Id, _tenantId));
        Assert.True(await _harness.Consumed.Any<IngestRequested>());
        Assert.True(await _harness.Published.Any<Fault<IngestRequested>>());

        var reloaded = await ReloadAsync(job.Id);
        Assert.Equal(VideoStage.Failed, reloaded.Stage);
        Assert.Contains("ffmpeg caiu", reloaded.FailureReason);
        Assert.Equal(Infrastructure.Messaging.MessagingExtensions.MaxRetries + 1, Behaviour.Executions);
    }

    public async ValueTask DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
