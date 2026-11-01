using System.Linq.Expressions;
using Esteira.Application.Abstractions;
using Esteira.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Infrastructure.Persistence;

/// <summary>
/// DbContext multi-tenant. Regras:
/// 1. Toda entidade que herda de <see cref="TenantEntity"/> recebe filtro global <c>TenantId == tenant atual</c>.
///    Sem tenant no contexto, o filtro compara com null e nenhuma linha é retornada.
/// 2. Ao salvar, entidades novas recebem o TenantId do contexto; sem tenant, SaveChanges falha.
/// 3. Tentar gravar entidade de outro tenant também falha.
/// </summary>
public sealed class EsteiraDbContext(DbContextOptions<EsteiraDbContext> options, ITenantContext tenant)
    : DbContext(options), IEsteiraDbContext
{
    private readonly ITenantContext _tenant = tenant;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<VideoJob> VideoJobs => Set<VideoJob>();
    public DbSet<StyleTemplate> StyleTemplates => Set<StyleTemplate>();
    public DbSet<SocialAccount> SocialAccounts => Set<SocialAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EsteiraDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(TenantEntity).IsAssignableFrom(entityType.ClrType) || entityType.IsOwned()) continue;
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(BuildTenantFilter(entityType.ClrType));
            modelBuilder.Entity(entityType.ClrType).HasIndex(nameof(TenantEntity.TenantId));
        }
    }

    // e => e.TenantId == _tenant.TenantId  (avaliado a cada query, por isso referencia o campo e não o valor)
    private LambdaExpression BuildTenantFilter(Type clrType)
    {
        var parameter = Expression.Parameter(clrType, "e");
        var tenantId = Expression.Convert(Expression.Property(parameter, nameof(TenantEntity.TenantId)), typeof(Guid?));
        var current = Expression.Property(Expression.Field(Expression.Constant(this), nameof(_tenant)), nameof(ITenantContext.TenantId));
        return Expression.Lambda(Expression.Equal(tenantId, current), parameter);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTenant();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyTenant();
        return base.SaveChanges();
    }

    private void ApplyTenant()
    {
        var entries = ChangeTracker.Entries<TenantEntity>().Where(e => e.State is EntityState.Added or EntityState.Modified).ToList();
        if (entries.Count == 0) return;
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("SaveChanges sem tenant no contexto");
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
                entry.Entity.TenantId = tenantId;
            if (entry.Entity.TenantId != tenantId)
                throw new InvalidOperationException($"Entidade {entry.Metadata.ClrType.Name} pertence a outro tenant");
            entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
