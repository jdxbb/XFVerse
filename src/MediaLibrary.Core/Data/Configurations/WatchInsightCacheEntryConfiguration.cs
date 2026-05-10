using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class WatchInsightCacheEntryConfiguration : IEntityTypeConfiguration<WatchInsightCacheEntry>
{
    public void Configure(EntityTypeBuilder<WatchInsightCacheEntry> builder)
    {
        builder.ToTable("WatchInsightCacheEntries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.ScopeKey)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.PayloadJson)
            .IsRequired();

        builder.Property(x => x.SourceFingerprint)
            .IsRequired()
            .HasMaxLength(1600);

        builder.Property(x => x.LastError)
            .HasMaxLength(2000);

        builder.HasIndex(x => new { x.Kind, x.ScopeKey })
            .IsUnique();

        builder.HasIndex(x => x.Kind);
        builder.HasIndex(x => x.IsStale);
        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}
