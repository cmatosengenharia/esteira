namespace Esteira.Domain.Entities;

public sealed class Tenant : Entity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public bool IsActive { get; set; } = true;
}
