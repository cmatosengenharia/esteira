namespace Esteira.Api.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "esteira";
    public string Audience { get; set; } = "esteira";
    /// <summary>Chave simétrica HS256. Em produção vem de segredo; o valor do appsettings é só para desenvolvimento.</summary>
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiresHours { get; set; } = 24 * 30;
}
