using Esteira.Domain.Entities;
using Esteira.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Infrastructure.Persistence.Configurations;

public sealed class VideoJobConfiguration : IEntityTypeConfiguration<VideoJob>
{
    public void Configure(EntityTypeBuilder<VideoJob> b)
    {
        b.ToTable("video_jobs");
        b.Property(x => x.OriginalFileName).HasMaxLength(500);
        b.Property(x => x.SourceObjectKey).HasMaxLength(1000);
        b.Property(x => x.AudioObjectKey).HasMaxLength(1000);
        b.Property(x => x.Stage).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.FailureReason).HasMaxLength(2000);
        b.Property(x => x.TranscriptJson).HasColumnType("jsonb");
        b.Property(x => x.EdlJson).HasColumnType("jsonb");
        b.HasIndex(x => new { x.TenantId, x.Stage });

        b.OwnsOne(x => x.Budget, o =>
        {
            o.Property(p => p.MaxLlmTokens).HasColumnName("budget_max_llm_tokens");
            o.Property(p => p.MaxRenderMinutes).HasColumnName("budget_max_render_minutes");
        });
        b.OwnsOne(x => x.Usage, o =>
        {
            o.Property(p => p.LlmTokens).HasColumnName("usage_llm_tokens");
            o.Property(p => p.RenderMinutes).HasColumnName("usage_render_minutes");
            o.Property(p => p.TranscribedMinutes).HasColumnName("usage_transcribed_minutes");
        });
        b.Navigation(x => x.Budget).IsRequired();
        b.Navigation(x => x.Usage).IsRequired();

        b.OwnsMany(x => x.Outputs, o =>
        {
            o.ToJson("outputs");
            o.Property(p => p.Kind).HasConversion<string>();
            o.Property(p => p.Aspect).HasConversion<string>();
        });
        b.OwnsMany(x => x.Timings, o =>
        {
            o.ToJson("timings");
            o.Property(p => p.Stage).HasConversion<string>();
        });

        // Targets: lista de enums gravada como "YouTube,TikTok" (portável entre Postgres e SQLite dos testes).
        b.Property(x => x.Targets)
            .HasConversion(
                v => string.Join(',', v.Select(n => n.ToString())),
                v => v.Length == 0 ? new List<SocialNetwork>() : v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<SocialNetwork>).ToList(),
                new ValueComparer<List<SocialNetwork>>(
                    (a, c) => a!.SequenceEqual(c!),
                    v => v.Aggregate(0, (h, n) => HashCode.Combine(h, n)),
                    v => v.ToList()))
            .HasMaxLength(400)
            .HasColumnName("targets");
    }
}
