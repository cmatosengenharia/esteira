namespace Esteira.Domain.ValueObjects;

/// <summary>Consumo acumulado do job. Owned type no EF.</summary>
public sealed class JobUsage
{
    public int LlmTokens { get; set; }
    public double RenderMinutes { get; set; }
    public double TranscribedMinutes { get; set; }
}
