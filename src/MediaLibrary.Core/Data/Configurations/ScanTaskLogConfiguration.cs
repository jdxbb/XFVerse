using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class ScanTaskLogConfiguration : IEntityTypeConfiguration<ScanTaskLog>
{
    public void Configure(EntityTypeBuilder<ScanTaskLog> builder)
    {
        builder.ToTable("ScanTaskLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(4000);

        builder.Property(x => x.SourceBaseUrlSnapshot)
            .HasMaxLength(500);

        builder.Property(x => x.SourceUsernameSnapshot)
            .HasMaxLength(200);

        builder.Property(x => x.ScanPathSnapshot)
            .HasMaxLength(1000);

        builder.Property(x => x.ScanPathDisplayNameSnapshot)
            .HasMaxLength(200);

        builder.Property(x => x.ReasonSummaryJson)
            .HasMaxLength(12000);

        builder.HasIndex(x => x.CreatedAt);
    }
}
