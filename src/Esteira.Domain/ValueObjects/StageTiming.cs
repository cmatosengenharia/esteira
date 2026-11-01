using Esteira.Domain.Enums;

namespace Esteira.Domain.ValueObjects;

/// <summary>Tempo gasto em uma etapa. Coleção JSON em VideoJob.</summary>
public sealed class StageTiming
{
    public VideoStage Stage { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public long? DurationMs { get; set; }
}
