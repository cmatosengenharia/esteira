using Esteira.Application.Abstractions;
using Esteira.Infrastructure.Persistence;
using Esteira.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEsteiraPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var cs = configuration.GetConnectionString("Postgres")
                 ?? throw new InvalidOperationException("ConnectionStrings:Postgres não configurada");

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());
        services.AddSingleton<IClock, SystemClock>();

        services.AddDbContext<EsteiraDbContext>(o => o.UseNpgsql(cs));
        services.AddScoped<IEsteiraDbContext>(sp => sp.GetRequiredService<EsteiraDbContext>());
        return services;
    }
}
