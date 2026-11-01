using Esteira.Application.Abstractions;

namespace Esteira.Infrastructure.Tenancy;

/// <summary>Registrado como scoped: um por requisição HTTP ou por mensagem consumida.</summary>
public sealed class TenantContext : ITenantContext, ITenantContextSetter
{
    public Guid? TenantId { get; private set; }

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId vazio", nameof(tenantId));
        TenantId = tenantId;
    }
}
