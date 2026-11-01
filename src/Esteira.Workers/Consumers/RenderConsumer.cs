using Esteira.Application.Abstractions;
using Esteira.Application.Messaging;
using Esteira.Domain.Entities;
using Esteira.Domain.Enums;

namespace Esteira.Workers.Consumers;

/// <summary>Fila `render`: EDL → masters via ffmpeg. Checa o orçamento de minutos de render antes de começar.</summary>
public sealed class RenderConsumer(
    IEsteiraDbContext db, ITenantContextSetter tenantSetter, IClock clock, ILogger<RenderConsumer> logger,
    IObjectStorage storage, IRenderer renderer, IUsageMeter usage)
    : StageConsumer<RenderRequested>(db, tenantSetter, clock, logger)
{
    protected override VideoStage TargetStage => VideoStage.Rendered;

    // Estimativa conservadora: render costuma custar ~1x a duração do vídeo por saída.
    protected override void EnsureBudget(VideoJob job) => job.EnsureRenderBudget((job.DurationSeconds ?? 0) / 60.0);

    protected override async Task<object?> ExecuteAsync(VideoJob job, CancellationToken ct)
    {
        var edl = job.EdlJson ?? throw new Domain.Exceptions.DomainException("Job sem EDL");
        var sourcePath = await storage.GetLocalPathAsync(job.SourceObjectKey, ct);

        var result = await renderer.RenderAsync(new RenderRequest(job, edl, sourcePath), ct);
        usage.RecordRenderMinutes(job, result.RenderMinutes, "ffmpeg masters");
        job.Outputs.AddRange(result.Outputs);

        return null; // fim da Fase 0: Copy/Approval/Publish entram depois
    }
}
