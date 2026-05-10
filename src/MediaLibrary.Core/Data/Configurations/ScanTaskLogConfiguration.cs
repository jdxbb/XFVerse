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

        builder.HasIndex(x => x.CreatedAt);
    }
}
