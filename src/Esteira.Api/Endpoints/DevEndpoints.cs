using Esteira.Api.Auth;
using Esteira.Application.Abstractions;
using Esteira.Domain.Entities;
using Esteira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Api.Endpoints;

/// <summary>Só em Development: cria tenant + usuário e devolve um JWT para usar na API.</summary>
public static class DevEndpoints
{
    public sealed record SeedRequest(string? TenantSlug, string? Email);
    public sealed record SeedResponse(Guid TenantId, Guid UserId, string Token);

    public static IEndpointRouteBuilder MapDevEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/dev/seed", async (SeedRequest? body, EsteiraDbContext db, ITenantContextSetter setter,
            JwtTokenService jwt, IClock clock, CancellationToken ct) =>
        {
            var slug = string.IsNullOrWhiteSpace(body?.TenantSlug) ? "dev" : body!.TenantSlug!.Trim();
            var email = string.IsNullOrWhiteSpace(body?.Email) ? "dev@esteira.local" : body!.Email!.Trim();

            // Tenant não é TenantEntity: consulta sem filtro é legítima aqui.
            var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Slug == slug, ct);
            if (tenant is null)
            {
                tenant = new Tenant { Name = slug, Slug = slug };
                db.Tenants.Add(tenant);
                await db.SaveChangesAsync(ct);
            }

            setter.SetTenant(tenant.Id);
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
            if (user is null)
            {
                user = new AppUser { Email = email, DisplayName = email };
                db.Users.Add(user);
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok(new SeedResponse(tenant.Id, user.Id, jwt.Issue(user, clock.UtcNow)));
        }).AllowAnonymous();

        return app;
    }
}
