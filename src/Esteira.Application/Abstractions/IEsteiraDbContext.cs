using Esteira.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Application.Abstractions;

/// <summary>Acesso a dados já filtrado por tenant (filtro global aplicado pela Infrastructure).</summary>
public interface IEsteiraDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<AppUser> Users { get; }
    DbSet<VideoJob> VideoJobs { get; }
    DbSet<StyleTemplate> StyleTemplates { get; }
    DbSet<SocialAccount> SocialAccounts { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
