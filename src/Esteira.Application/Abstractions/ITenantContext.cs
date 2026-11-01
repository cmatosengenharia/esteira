namespace Esteira.Application.Abstractions;

/// <summary>Tenant da requisição ou da mensagem em processamento. Null = sem tenant: nenhuma query retorna dados.</summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
}

/// <summary>Usado pela API (a partir do JWT) e pelos workers (a partir da mensagem) para fixar o tenant do escopo.</summary>
public interface ITenantContextSetter
{
    void SetTenant(Guid tenantId);
}
