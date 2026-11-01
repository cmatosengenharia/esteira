using Esteira.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants");
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Slug).HasMaxLength(100);
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.ToTable("users");
        b.Property(x => x.Email).HasMaxLength(320);
        b.Property(x => x.DisplayName).HasMaxLength(200);
        b.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
    }
}

public sealed class StyleTemplateConfiguration : IEntityTypeConfiguration<StyleTemplate>
{
    public void Configure(EntityTypeBuilder<StyleTemplate> b)
    {
        b.ToTable("style_templates");
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Niche).HasMaxLength(200);
        b.Property(x => x.ReferenceUrl).HasMaxLength(2000);
        b.Property(x => x.StyleBibleJson).HasColumnType("jsonb");
    }
}

public sealed class SocialAccountConfiguration : IEntityTypeConfiguration<SocialAccount>
{
    public void Configure(EntityTypeBuilder<SocialAccount> b)
    {
        b.ToTable("social_accounts");
        b.Property(x => x.Network).HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ExternalAccountId).HasMaxLength(200);
        b.Property(x => x.DisplayName).HasMaxLength(200);
        b.HasIndex(x => new { x.TenantId, x.Network, x.ExternalAccountId }).IsUnique();
    }
}
