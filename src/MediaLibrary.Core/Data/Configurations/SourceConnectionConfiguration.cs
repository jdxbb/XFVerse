using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class SourceConnectionConfiguration : IEntityTypeConfiguration<SourceConnection>
{
    public void Configure(EntityTypeBuilder<SourceConnection> builder)
    {
        builder.ToTable("SourceConnections");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(x => x.BaseUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Username)
            .HasMaxLength(200);

        builder.Property(x => x.PasswordEncrypted)
            .HasMaxLength(1000);

        builder.HasMany(x => x.ScanPaths)
            .WithOne(x => x.SourceConnection)
            .HasForeignKey(x => x.SourceConnectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.MediaFiles)
            .WithOne(x => x.SourceConnection)
            .HasForeignKey(x => x.SourceConnectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.ScanTaskLogs)
            .WithOne(x => x.SourceConnection)
            .HasForeignKey(x => x.SourceConnectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
