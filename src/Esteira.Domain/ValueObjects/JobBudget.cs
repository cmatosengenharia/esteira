namespace Esteira.Domain.ValueObjects;

/// <summary>Teto de custo do job. Owned type no EF.</summary>
public sealed class JobBudget
{
    public int MaxLlmTokens { get; set; }
    public double MaxRenderMinutes { get; set; }

    public static JobBudget Default => new() { MaxLlmTokens = 200_000, MaxRenderMinutes = 30 };
}
