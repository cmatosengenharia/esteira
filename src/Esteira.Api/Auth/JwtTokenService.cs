using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Esteira.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Esteira.Api.Auth;

public static class EsteiraClaims
{
    public const string TenantId = "tenant_id";
}

public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _o = options.Value;

    public string Issue(AppUser user, DateTimeOffset now)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(EsteiraClaims.TenantId, user.TenantId.ToString()),
        };
        var token = new JwtSecurityToken(_o.Issuer, _o.Audience, claims,
            notBefore: now.UtcDateTime, expires: now.AddHours(_o.ExpiresHours).UtcDateTime, signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
