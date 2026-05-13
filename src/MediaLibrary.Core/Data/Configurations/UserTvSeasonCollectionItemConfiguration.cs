using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class UserTvSeasonCollectionItemConfiguration : IEntityTypeConfiguration<UserTvSeasonCollectionItem>
{
    public void Configure(EntityTypeBuilder<UserTvSeasonCollectionItem> builder)
    {
        builder.ToTable("UserTvSeasonCollectionItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SeriesTitle)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(x => x.OriginalSeriesTitle)
            .HasMaxLength(300);

        builder.Property(x => x.SeasonTitle)
            .HasMaxLength(300);

        builder.Property(x => x.PosterRemoteUrl)
            .HasMaxLength(1200);

        builder.Property(x => x.Overview)
            .HasMaxLength(5000);

        builder.Property(x => x.GenresText)
            .HasMaxLength(1000);

        builder.Property(x => x.Country)
            .HasMaxLength(120);

        builder.Property(x => x.Language)
            .HasMaxLength(120);

        builder.HasOne<TvSeason>()
            .WithMany(x => x.CollectionItems)
            .HasForeignKey(x => x.TvSeasonId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.TvSeasonId);
        builder.HasIndex(x => x.TvSeriesId);
        builder.HasIndex(x => x.TmdbSeriesId);
        builder.HasIndex(x => x.TmdbSeasonId);
        builder.HasIndex(x => new { x.TmdbSeriesId, x.SeasonNumber });
        builder.HasIndex(x => x.IsFavorite);
        builder.HasIndex(x => x.IsWantToWatch);
        builder.HasIndex(x => x.IsNotInterested);
        builder.HasIndex(x => x.UpdatedAt);
    }
}
