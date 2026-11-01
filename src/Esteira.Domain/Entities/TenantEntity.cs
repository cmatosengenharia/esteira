namespace Esteira.Domain.Entities;

/// <summary>Toda entidade de negócio pertence a um tenant. O DbContext filtra por TenantId em toda query.</summary>
public abstract class TenantEntity : Entity
{
    public Guid TenantId { get; set; }
}
