using Esteira.Application.Abstractions;

namespace Esteira.Api.Auth;

/// <summary>Lê o claim tenant_id do JWT já validado e fixa o tenant do escopo da requisição.</summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContextSetter setter)
    {
        var claim = context.User.FindFirst(EsteiraClaims.TenantId)?.Value;
        if (Guid.TryParse(claim, out var tenantId))
            setter.SetTenant(tenantId);
        await next(context);
    }
}
