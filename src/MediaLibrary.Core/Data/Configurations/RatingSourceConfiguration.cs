using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class RatingSourceConfiguration : IEntityTypeConfiguration<RatingSource>
{
    public void Configure(EntityTypeBuilder<RatingSource> builder)
    {
        builder.ToTable("RatingSources");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.SourceUrl)
            .HasMaxLength(1200);

        builder.HasIndex(x => new { x.MovieId, x.SourceName })
            .IsUnique();
    }
}
