using Esteira.Domain.Entities;

namespace Esteira.Application.Abstractions;

public sealed record PlanRequest(VideoJob Job, Transcript Transcript, string WavLocalPath, string? StyleBibleJson);

/// <summary>Produz o EDL (docs/contracts/edl.schema.json) como JSON validado contra o schema.</summary>
public interface IEditPlanner
{
    Task<string> PlanAsync(PlanRequest request, CancellationToken cancellationToken);
}
