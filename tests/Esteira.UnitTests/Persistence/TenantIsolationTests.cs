using Esteira.Application.Abstractions;
using Esteira.Domain.Entities;
using Esteira.Domain.Enums;
using Esteira.Infrastructure.Persistence;
using Esteira.Infrastructure.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Esteira.UnitTests.Persistence;

/// <summary>Prova a regra "nenhuma query sem tenant" do CLAUDE.md usando SQLite em memória.</summary>
public sealed class TenantIsolationTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TenantIsolationTests()
    {
        _connection.Open();
        using var db = NewContext(new TenantContext());
        db.Database.EnsureCreated();
    }

    private EsteiraDbContext NewContext(ITenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<EsteiraDbContext>().UseSqlite(_connection).Options;
        return new EsteiraDbContext(options, tenant);
    }

    private static TenantContext For(Guid tenantId)
    {
        var t = new TenantContext();
        t.SetTenant(tenantId);
        return t;
    }

    private static VideoJob NewJob(string name) => new()
    {
        OriginalFileName = name,
        SourceObjectKey = $"source/{name}",
        Targets = [SocialNetwork.YouTubeShorts, SocialNetwork.TikTok],
    };

    [Fact]
    public async Task Query_without_tenant_returns_nothing_and_tenants_do_not_see_each_other()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var db = NewContext(For(tenantA)))
        {
            db.VideoJobs.Add(NewJob("a1.mp4"));
            db.VideoJobs.Add(NewJob("a2.mp4"));
            await db.SaveChangesAsync(CancellationToken.None);
        }
        await using (var db = NewContext(For(tenantB)))
        {
            db.VideoJobs.Add(NewJob("b1.mp4"));
            await db.SaveChangesAsync(CancellationToken.None);
        }

        await using (var db = NewContext(new TenantContext()))
        {
            Assert.Empty(await db.VideoJobs.ToListAsync());
            Assert.Equal(0, await db.VideoJobs.CountAsync());
        }
        await using (var db = NewContext(For(tenantA)))
        {
            var jobs = await db.VideoJobs.OrderBy(j => j.OriginalFileName).ToListAsync();
            Assert.Equal(new[] { "a1.mp4", "a2.mp4" }, jobs.Select(j => j.OriginalFileName));
            Assert.All(jobs, j => Assert.Equal(tenantA, j.TenantId));
        }
        await using (var db = NewContext(For(tenantB)))
        {
            var only = await db.VideoJobs.SingleAsync();
            Assert.Equal("b1.mp4", only.OriginalFileName);
            Assert.Equal(new List<SocialNetwork> { SocialNetwork.YouTubeShorts, SocialNetwork.TikTok }, only.Targets);
        }
    }

    [Fact]
    public async Task SaveChanges_without_tenant_fails()
    {
        await using var db = NewContext(new TenantContext());
        db.VideoJobs.Add(NewJob("x.mp4"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Saving_entity_of_another_tenant_fails()
    {
        await using var db = NewContext(For(Guid.NewGuid()));
        var job = NewJob("y.mp4");
        job.TenantId = Guid.NewGuid();
        db.VideoJobs.Add(job);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Owned_json_collections_round_trip()
    {
        var tenant = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Guid id;
        await using (var db = NewContext(For(tenant)))
        {
            var job = NewJob("z.mp4");
            job.BeginStage(VideoStage.Ingested, now);
            job.CompleteStage(VideoStage.Ingested, now.AddSeconds(3));
            job.Outputs.Add(new() { Kind = OutputKind.Master, Aspect = AspectRatio.Vertical9x16, ObjectKey = "out/z.mp4", DurationSeconds = 12.5, SizeBytes = 1000 });
            db.VideoJobs.Add(job);
            await db.SaveChangesAsync(CancellationToken.None);
            id = job.Id;
        }
        await using (var db = NewContext(For(tenant)))
        {
            var job = await db.VideoJobs.SingleAsync(j => j.Id == id);
            Assert.Equal(VideoStage.Ingested, job.Stage);
            var timing = Assert.Single(job.Timings);
            Assert.Equal(3000, timing.DurationMs);
            var output = Assert.Single(job.Outputs);
            Assert.Equal(AspectRatio.Vertical9x16, output.Aspect);
        }
    }

    public void Dispose() => _connection.Dispose();
}
