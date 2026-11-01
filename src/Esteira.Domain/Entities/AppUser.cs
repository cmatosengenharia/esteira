namespace Esteira.Domain.Entities;

/// <summary>Usuário de um tenant. Fase 0: só identidade; senha/SSO entram depois.</summary>
public sealed class AppUser : TenantEntity
{
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
}
