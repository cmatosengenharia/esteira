using Esteira.Domain.Enums;

namespace Esteira.Domain.ValueObjects;

/// <summary>Arquivo produzido pelo render. Coleção JSON em VideoJob.</summary>
public sealed class JobOutput
{
    public OutputKind Kind { get; set; }
    public AspectRatio Aspect { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public double DurationSeconds { get; set; }
    public long SizeBytes { get; set; }
}
