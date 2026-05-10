using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class ExternalMetadataCacheConfiguration : IEntityTypeConfiguration<ExternalMetadataCache>
{
    public void Configure(EntityTypeBuilder<ExternalMetadataCache> builder)
    {
        builder.ToTable("ExternalMetadataCache");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.CacheType)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.CacheKey)
            .IsRequired()
            .HasMaxLength(1600);

        builder.Property(x => x.PayloadJson)
            .IsRequired();

        builder.HasIndex(x => new { x.Provider, x.CacheType, x.CacheKey })
            .IsUnique();

        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}
