using Esteira.Application.Abstractions;
using Esteira.Application.Metrics;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Api.Endpoints;

public static class MetricsEndpoints
{
    public static IEndpointRouteBuilder MapMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/metrics").RequireAuthorization();

        group.MapGet("/weekly", async (int? weeks, IEsteiraDbContext db, IClock clock, CancellationToken ct) =>
        {
            var count = weeks ?? 4;
            if (count is < 1 or > 12)
                return Results.BadRequest(new { error = "weeks deve estar entre 1 e 12" });

            var now = clock.UtcNow;
            var since = WeeklyMetrics.WeekStart(now).AddDays(-7 * (count - 1));
            var jobs = await db.VideoJobs.Where(j => j.UpdatedAt >= since).ToListAsync(ct);
            return Results.Ok(WeeklyMetrics.Compute(jobs, now, count));
        });

        return app;
    }
}
