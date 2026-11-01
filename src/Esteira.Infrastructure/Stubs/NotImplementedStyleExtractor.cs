using Esteira.Application.Abstractions;

namespace Esteira.Infrastructure.Stubs;

/// <summary>Entra na Fase 1 (ffmpeg scene/silencedetect/ebur128 + LLM de visão).</summary>
public sealed class NotImplementedStyleExtractor : IStyleExtractor
{
    public Task<string> ExtractAsync(string referenceUrl, string niche, CancellationToken cancellationToken)
        => throw new NotImplementedException("IStyleExtractor entra na Fase 1: extração de molde a partir de um viral (ver docs/FASE_1.md).");
}

/// <summary>Planner com LLM entra na Fase 1. A Fase 0 usa o SilenceEditPlanner (sem LLM).</summary>
public sealed class NotImplementedLlmEditPlanner : IEditPlanner
{
    public Task<string> PlanAsync(PlanRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException("IEditPlanner com Claude API entra na Fase 1 (ver docs/FASE_1.md). Fase 0 usa SilenceEditPlanner.");
}
