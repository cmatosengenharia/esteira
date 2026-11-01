using Esteira.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Esteira.Infrastructure.Persistence;

/// <summary>Usado só pelo `dotnet ef` (design-time). Não precisa de banco de pé para gerar migrations.</summary>
public sealed class EsteiraDbContextFactory : IDesignTimeDbContextFactory<EsteiraDbContext>
{
    public EsteiraDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                 ?? "Host=localhost;Database=esteira;Username=esteira;Password=esteira";
        var options = new DbContextOptionsBuilder<EsteiraDbContext>().UseNpgsql(cs).Options;
        return new EsteiraDbContext(options, new NoTenant());
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
    }
}
