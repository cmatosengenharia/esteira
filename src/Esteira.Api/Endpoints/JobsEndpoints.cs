using Esteira.Application.Abstractions;
using Esteira.Application.Contracts;
using Esteira.Application.Messaging;
using Esteira.Domain.Entities;
using Esteira.Domain.Enums;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Api.Endpoints;

public static class JobsEndpoints
{
    public sealed record TimingDto(string Stage, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, long? DurationMs);
    public sealed record OutputDto(string Kind, string Aspect, string ObjectKey, string? LocalPath, double DurationSeconds, long SizeBytes);
    public sealed record BudgetDto(int MaxLlmTokens, double MaxRenderMinutes);
    public sealed record UsageDto(int LlmTokens, double RenderMinutes, double TranscribedMinutes);
    public sealed record JobDto(
        Guid Id, string Stage, string? FailureReason, string OriginalFileName, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
        double? DurationSeconds, double? Fps, IReadOnlyList<string> Targets, Guid? StyleTemplateId,
        BudgetDto Budget, UsageDto Usage, IReadOnlyList<TimingDto> Timings, IReadOnlyList<OutputDto> Outputs);

    private static readonly string[] AllowedExtensions = [".mp4", ".mov", ".m4v", ".mkv", ".webm"];

    public static IEndpointRouteBuilder MapJobsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/jobs").RequireAuthorization();

        group.MapPost("/", async (IFormFile file, string? targets, Guid? styleTemplateId, int? maxLlmTokens, double? maxRenderMinutes,
            IEsteiraDbContext db, ITenantContext tenant, IObjectStorage storage, IPublishEndpoint bus, CancellationToken ct) =>
        {
            var tenantId = tenant.TenantId ?? throw new InvalidOperationException("Sem tenant");
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return Results.BadRequest(new { error = $"Extensão {ext} não suportada. Use {string.Join(", ", AllowedExtensions)}" });

            var job = new VideoJob
            {
                OriginalFileName = Path.GetFileName(file.FileName),
                SourceObjectKey = string.Empty,
                StyleTemplateId = styleTemplateId,
                Targets = ParseTargets(targets),
            };
            job.SourceObjectKey = ObjectKeys.Source(tenantId, job.Id, ext);
            if (maxLlmTokens is { } tokens) job.Budget.MaxLlmTokens = tokens;
            if (maxRenderMinutes is { } minutes) job.Budget.MaxRenderMinutes = minutes;

            if (styleTemplateId is { } templateId && !await db.StyleTemplates.AnyAsync(t => t.Id == templateId, ct))
                return Results.BadRequest(new { error = $"StyleTemplate {templateId} não encontrado" });

            await using (var stream = file.OpenReadStream())
                await storage.PutAsync(job.SourceObjectKey, stream, ct);

            db.VideoJobs.Add(job);
            await db.SaveChangesAsync(ct);
            await bus.Publish(new IngestRequested(job.Id, tenantId), ct);

            return Results.Created($"/jobs/{job.Id}", await ToDto(job, storage, ct));
        }).DisableAntiforgery();

        group.MapGet("/", async (IEsteiraDbContext db, IObjectStorage storage, CancellationToken ct) =>
        {
            var jobs = await db.VideoJobs.OrderByDescending(j => j.CreatedAt).Take(100).ToListAsync(ct);
            var dtos = new List<JobDto>(jobs.Count);
            foreach (var j in jobs) dtos.Add(await ToDto(j, storage, ct));
            return Results.Ok(dtos);
        });

        group.MapGet("/{id:guid}", async (Guid id, IEsteiraDbContext db, IObjectStorage storage, CancellationToken ct) =>
        {
            var job = await db.VideoJobs.SingleOrDefaultAsync(j => j.Id == id, ct);
            return job is null ? Results.NotFound() : Results.Ok(await ToDto(job, storage, ct));
        });

        group.MapGet("/{id:guid}/edl", async (Guid id, IEsteiraDbContext db, CancellationToken ct) =>
        {
            var job = await db.VideoJobs.SingleOrDefaultAsync(j => j.Id == id, ct);
            return job?.EdlJson is null ? Results.NotFound() : Results.Content(job.EdlJson, "application/json");
        });

        group.MapGet("/{id:guid}/transcript", async (Guid id, IEsteiraDbContext db, CancellationToken ct) =>
        {
            var job = await db.VideoJobs.SingleOrDefaultAsync(j => j.Id == id, ct);
            return job?.TranscriptJson is null ? Results.NotFound() : Results.Content(job.TranscriptJson, "application/json");
        });

        // Download de uma saída (master ou short) pelo índice em `outputs`; a interface web busca com o bearer e monta o blob.
        group.MapGet("/{id:guid}/outputs/{index:int}", async (Guid id, int index, IEsteiraDbContext db, IObjectStorage storage, CancellationToken ct) =>
        {
            var job = await db.VideoJobs.SingleOrDefaultAsync(j => j.Id == id, ct);
            if (job is null || index < 0 || index >= job.Outputs.Count) return Results.NotFound();
            var output = job.Outputs[index];
            if (!await storage.ExistsAsync(output.ObjectKey, ct)) return Results.NotFound();
            var name = OutputFiles.FileName(output.ObjectKey);
            var stream = await storage.OpenReadAsync(output.ObjectKey, ct);
            return Results.Stream(stream, OutputFiles.ContentType(name), fileDownloadName: name, enableRangeProcessing: true);
        });

        return app;
    }

    private static List<SocialNetwork> ParseTargets(string? targets)
    {
        if (string.IsNullOrWhiteSpace(targets)) return [SocialNetwork.YouTubeShorts];
        return targets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => Enum.TryParse<SocialNetwork>(t, true, out var n) ? n : throw new ArgumentException($"Rede desconhecida: {t}"))
            .Distinct().ToList();
    }

    private static async Task<JobDto> ToDto(VideoJob j, IObjectStorage storage, CancellationToken ct)
    {
        var outputs = new List<OutputDto>();
        foreach (var o in j.Outputs)
        {
            var local = await storage.ExistsAsync(o.ObjectKey, ct) ? await storage.GetLocalPathAsync(o.ObjectKey, ct) : null;
            outputs.Add(new OutputDto(o.Kind.ToString(), o.Aspect.ToContract(), o.ObjectKey, local, o.DurationSeconds, o.SizeBytes));
        }
        return new JobDto(
            j.Id, j.Stage.ToString(), j.FailureReason, j.OriginalFileName, j.CreatedAt, j.UpdatedAt,
            j.DurationSeconds, j.Fps, j.Targets.Select(t => t.ToString()).ToList(), j.StyleTemplateId,
            new BudgetDto(j.Budget.MaxLlmTokens, j.Budget.MaxRenderMinutes),
            new UsageDto(j.Usage.LlmTokens, j.Usage.RenderMinutes, j.Usage.TranscribedMinutes),
            j.Timings.Select(t => new TimingDto(t.Stage.ToString(), t.StartedAt, t.FinishedAt, t.DurationMs)).ToList(),
            outputs);
    }
}
