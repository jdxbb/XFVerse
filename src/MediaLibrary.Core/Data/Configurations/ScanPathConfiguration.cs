using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class ScanPathConfiguration : IEntityTypeConfiguration<ScanPath>
{
    public void Configure(EntityTypeBuilder<ScanPath> builder)
    {
        builder.ToTable("ScanPaths");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Path)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(x => new { x.SourceConnectionId, x.Path })
            .IsUnique();

        builder.HasMany(x => x.MediaFiles)
            .WithOne(x => x.ScanPath)
            .HasForeignKey(x => x.ScanPathId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.ScanTaskLogs)
            .WithOne(x => x.ScanPath)
            .HasForeignKey(x => x.ScanPathId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
