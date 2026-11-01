namespace Esteira.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
