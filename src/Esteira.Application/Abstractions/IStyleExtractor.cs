namespace Esteira.Application.Abstractions;

/// <summary>URL de um viral → StyleBible JSON (docs/contracts/style-bible.schema.json). Fase 1.</summary>
public interface IStyleExtractor
{
    Task<string> ExtractAsync(string referenceUrl, string niche, CancellationToken cancellationToken);
}
