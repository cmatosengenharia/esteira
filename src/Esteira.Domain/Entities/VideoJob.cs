using Esteira.Domain.Enums;
using Esteira.Domain.Exceptions;
using Esteira.Domain.ValueObjects;

namespace Esteira.Domain.Entities;

/// <summary>Um vídeo bruto atravessando a esteira. Workers avançam o Stage; nunca pulam etapas.</summary>
public sealed class VideoJob : TenantEntity
{
    public required string OriginalFileName { get; set; }
    public required string SourceObjectKey { get; set; }
    public VideoStage Stage { get; private set; } = VideoStage.Uploaded;
    public string? FailureReason { get; private set; }

    public JobBudget Budget { get; set; } = JobBudget.Default;
    public JobUsage Usage { get; set; } = new();
    public List<JobOutput> Outputs { get; set; } = [];
    public List<StageTiming> Timings { get; set; } = [];
    public List<SocialNetwork> Targets { get; set; } = [];
    public Guid? StyleTemplateId { get; set; }

    // Metadados preenchidos ao longo da esteira
    public double? DurationSeconds { get; set; }
    public double? Fps { get; set; }
    public string? AudioObjectKey { get; set; }
    public string? TranscriptJson { get; set; }
    public string? EdlJson { get; set; }

    /// <summary>Etapa que o próximo worker deve executar, dada a atual.</summary>
    public static VideoStage NextStage(VideoStage current) => current switch
    {
        VideoStage.Uploaded => VideoStage.Ingested,
        VideoStage.Ingested => VideoStage.Transcribed,
        VideoStage.Transcribed => VideoStage.Planned,
        VideoStage.Planned => VideoStage.Rendered,
        VideoStage.Rendered => VideoStage.CopyReady,
        VideoStage.CopyReady => VideoStage.AwaitingApproval,
        VideoStage.AwaitingApproval => VideoStage.Approved,
        VideoStage.Approved => VideoStage.Published,
        _ => throw new DomainException($"Não há próxima etapa depois de {current}"),
    };

    /// <summary>Marca o início de uma etapa. Idempotente: se já existe timing aberto para ela, reaproveita.</summary>
    public StageTiming BeginStage(VideoStage target, DateTimeOffset now)
    {
        if (Stage == VideoStage.Failed)
            throw new DomainException($"Job {Id} está em Failed e não pode avançar");
        var expected = NextStage(Stage);
        if (target != expected)
            throw new DomainException($"Job {Id} está em {Stage}; esperada etapa {expected}, recebida {target}");

        var open = Timings.FirstOrDefault(t => t.Stage == target && t.FinishedAt is null);
        if (open is not null) return open;
        var timing = new StageTiming { Stage = target, StartedAt = now };
        Timings.Add(timing);
        return timing;
    }

    public void CompleteStage(VideoStage target, DateTimeOffset now)
    {
        var timing = Timings.LastOrDefault(t => t.Stage == target && t.FinishedAt is null)
            ?? throw new DomainException($"Etapa {target} não foi iniciada no job {Id}");
        timing.FinishedAt = now;
        timing.DurationMs = (long)(now - timing.StartedAt).TotalMilliseconds;
        Stage = target;
        UpdatedAt = now;
    }

    public void Fail(string reason, DateTimeOffset now)
    {
        foreach (var t in Timings.Where(t => t.FinishedAt is null))
        {
            t.FinishedAt = now;
            t.DurationMs = (long)(now - t.StartedAt).TotalMilliseconds;
        }
        Stage = VideoStage.Failed;
        FailureReason = reason.Length > 2000 ? reason[..2000] : reason;
        UpdatedAt = now;
    }

    /// <summary>Já está na etapa pedida ou além dela: o worker deve ignorar a mensagem (idempotência).</summary>
    public bool AlreadyPast(VideoStage target) => Stage != VideoStage.Failed && Stage >= target;

    public void EnsureRenderBudget(double additionalMinutes)
    {
        var projected = Usage.RenderMinutes + additionalMinutes;
        if (projected > Budget.MaxRenderMinutes)
            throw new BudgetExceededException(Id, "render minutes", projected, Budget.MaxRenderMinutes);
    }

    public void EnsureLlmBudget(int additionalTokens)
    {
        var projected = Usage.LlmTokens + additionalTokens;
        if (projected > Budget.MaxLlmTokens)
            throw new BudgetExceededException(Id, "llm tokens", projected, Budget.MaxLlmTokens);
    }
}
