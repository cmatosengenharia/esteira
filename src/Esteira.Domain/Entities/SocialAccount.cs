using Esteira.Domain.Enums;

namespace Esteira.Domain.Entities;

/// <summary>Conta conectada em uma rede. Tokens sempre cifrados pela Infrastructure antes de chegar aqui.</summary>
public sealed class SocialAccount : TenantEntity
{
    public SocialNetwork Network { get; set; }
    public PublishMode Mode { get; set; }
    public required string ExternalAccountId { get; set; }
    public required string DisplayName { get; set; }
    public string? EncryptedAccessToken { get; set; }
    public string? EncryptedRefreshToken { get; set; }
    public DateTimeOffset? TokenExpiresAt { get; set; }
}
