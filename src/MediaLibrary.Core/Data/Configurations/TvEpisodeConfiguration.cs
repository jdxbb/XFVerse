using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class TvEpisodeConfiguration : IEntityTypeConfiguration<TvEpisode>
{
    public void Configure(EntityTypeBuilder<TvEpisode> builder)
    {
        builder.ToTable("TvEpisodes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(x => x.Overview)
            .HasMaxLength(5000);

        builder.Property(x => x.StillLocalPath)
            .HasMaxLength(1200);

        builder.Property(x => x.StillRemoteUrl)
            .HasMaxLength(1200);

        builder.HasIndex(x => x.TmdbEpisodeId);
        builder.HasIndex(x => x.DefaultMediaFileId);
        builder.HasIndex(x => x.IsWatched);
        builder.HasIndex(x => x.LastPlayedAt);
        builder.HasIndex(x => new { x.TvSeasonId, x.EpisodeNumber })
            .IsUnique();

        builder.HasMany(x => x.MediaFiles)
            .WithOne(x => x.Episode)
            .HasForeignKey(x => x.EpisodeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.WatchHistories)
            .WithOne(x => x.Episode)
            .HasForeignKey(x => x.EpisodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
