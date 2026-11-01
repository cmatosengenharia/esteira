namespace Esteira.Domain.Exceptions;

/// <summary>Lançada por um worker quando o job já consumiu (ou consumiria) mais do que o orçamento permite.</summary>
public sealed class BudgetExceededException(Guid jobId, string resource, double used, double limit)
    : Exception($"Job {jobId} estourou o orçamento de {resource}: usado {used:0.##}, limite {limit:0.##}")
{
    public Guid JobId { get; } = jobId;
    public string Resource { get; } = resource;
    public double Used { get; } = used;
    public double Limit { get; } = limit;
}
