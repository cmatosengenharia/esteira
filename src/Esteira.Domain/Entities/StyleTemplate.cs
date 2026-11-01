namespace Esteira.Domain.Entities;

/// <summary>Molde extraído de um viral (docs/contracts/style-bible.schema.json) guardado como JSON.</summary>
public sealed class StyleTemplate : TenantEntity
{
    public required string Name { get; set; }
    public required string Niche { get; set; }
    public string? ReferenceUrl { get; set; }
    public required string StyleBibleJson { get; set; }
}
