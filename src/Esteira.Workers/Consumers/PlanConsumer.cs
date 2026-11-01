using System.Text.Json;
using Esteira.Application.Abstractions;
using Esteira.Application.Contracts;
using Esteira.Application.Messaging;
using Esteira.Domain.Entities;
using Esteira.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Workers.Consumers;

/// <summary>Fila `plan`: transcrição + molde → EDL validado contra o schema. Fase 0 usa o planner por silêncio (sem LLM).</summary>
public sealed class PlanConsumer(
    IEsteiraDbContext db, ITenantContextSetter tenantSetter, IClock clock, ILogger<PlanConsumer> logger,
    IObjectStorage storage, IEditPlanner planner)
    : StageConsumer<PlanRequested>(db, tenantSetter, clock, logger)
{
    protected override VideoStage TargetStage => VideoStage.Planned;

    protected override void EnsureBudget(VideoJob job) => job.EnsureLlmBudget(0); // planner da Fase 0 não gasta tokens

    protected override async Task<object?> ExecuteAsync(VideoJob job, CancellationToken ct)
    {
        var transcriptJson = job.TranscriptJson ?? throw new Domain.Exceptions.DomainException("Job sem transcrição");
        var transcript = JsonSerializer.Deserialize<Transcript>(transcriptJson, JsonDefaults.Options)
            ?? throw new Domain.Exceptions.DomainException("Transcrição inválida");
        var wavPath = await storage.GetLocalPathAsync(job.AudioObjectKey!, ct);

        string? styleBible = null;
        if (job.StyleTemplateId is { } templateId)
        {
            var template = await Db.StyleTemplates.SingleOrDefaultAsync(t => t.Id == templateId, ct)
                ?? throw new Domain.Exceptions.DomainException($"StyleTemplate {templateId} não encontrado");
            styleBible = template.StyleBibleJson;
        }

        job.EdlJson = await planner.PlanAsync(new PlanRequest(job, transcript, wavPath, styleBible), ct);
        // cópia em arquivo ao lado dos masters, para inspeção e para o render de outras ferramentas
        await using (var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(job.EdlJson)))
            await storage.PutAsync(ObjectKeys.Edl(job), ms, ct);
        return new RenderRequested(job.Id, job.TenantId);
    }
}
