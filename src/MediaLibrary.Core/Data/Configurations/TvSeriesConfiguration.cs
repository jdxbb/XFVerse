using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class TvSeriesConfiguration : IEntityTypeConfiguration<TvSeries>
{
    public void Configure(EntityTypeBuilder<TvSeries> builder)
    {
        builder.ToTable("TvSeries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(x => x.OriginalName)
            .HasMaxLength(300);

        builder.Property(x => x.Overview)
            .HasMaxLength(5000);

        builder.Property(x => x.PosterLocalPath)
            .HasMaxLength(1200);

        builder.Property(x => x.PosterRemoteUrl)
            .HasMaxLength(1200);

        builder.Property(x => x.Country)
            .HasMaxLength(120);

        builder.Property(x => x.Language)
            .HasMaxLength(120);

        builder.Property(x => x.GenresText)
            .HasMaxLength(1000);

        builder.Property(x => x.DirectorText)
            .HasMaxLength(1000);

        builder.Property(x => x.WriterText)
            .HasMaxLength(1000);

        builder.Property(x => x.ActorsText)
            .HasMaxLength(1000);

        builder.Property(x => x.ProductionStatus)
            .HasMaxLength(120);

        builder.Property(x => x.NetworksText)
            .HasMaxLength(1000);

        builder.Property(x => x.ProductionCompaniesText)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.TmdbSeriesId)
            .IsUnique();

        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.FirstAirYear);

        builder.HasMany(x => x.Seasons)
            .WithOne(x => x.Series)
            .HasForeignKey(x => x.TvSeriesId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
