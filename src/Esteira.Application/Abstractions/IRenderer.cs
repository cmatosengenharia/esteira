using Esteira.Domain.Entities;
using Esteira.Domain.ValueObjects;

namespace Esteira.Application.Abstractions;

public sealed record RenderRequest(VideoJob Job, string EdlJson, string SourceLocalPath);

public sealed record RenderResult(IReadOnlyList<JobOutput> Outputs, double RenderMinutes);

/// <summary>EDL → masters. Fase 0: ffmpeg com select/concat + legendas ASS; Hyperframes entra para composições HTML.</summary>
public interface IRenderer
{
    Task<RenderResult> RenderAsync(RenderRequest request, CancellationToken cancellationToken);
}
