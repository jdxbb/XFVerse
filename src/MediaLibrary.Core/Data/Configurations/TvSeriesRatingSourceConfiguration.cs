using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class TvSeriesRatingSourceConfiguration : IEntityTypeConfiguration<TvSeriesRatingSource>
{
    public void Configure(EntityTypeBuilder<TvSeriesRatingSource> builder)
    {
        builder.ToTable("TvSeriesRatingSources");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.SourceUrl)
            .HasMaxLength(1200);

        builder.HasIndex(x => new { x.TvSeriesId, x.SourceName })
            .IsUnique();

        builder.HasOne(x => x.Series)
            .WithMany(x => x.RatingSources)
            .HasForeignKey(x => x.TvSeriesId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
