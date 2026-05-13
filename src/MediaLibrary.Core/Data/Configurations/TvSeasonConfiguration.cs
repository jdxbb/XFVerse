using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class TvSeasonConfiguration : IEntityTypeConfiguration<TvSeason>
{
    public void Configure(EntityTypeBuilder<TvSeason> builder)
    {
        builder.ToTable("TvSeasons");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(x => x.Overview)
            .HasMaxLength(5000);

        builder.Property(x => x.PosterLocalPath)
            .HasMaxLength(1200);

        builder.Property(x => x.PosterRemoteUrl)
            .HasMaxLength(1200);

        builder.HasIndex(x => x.TmdbSeasonId);
        builder.HasIndex(x => x.IdentificationStatus);
        builder.HasIndex(x => new { x.TvSeriesId, x.SeasonNumber })
            .IsUnique();

        builder.HasMany(x => x.Episodes)
            .WithOne(x => x.Season)
            .HasForeignKey(x => x.TvSeasonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
