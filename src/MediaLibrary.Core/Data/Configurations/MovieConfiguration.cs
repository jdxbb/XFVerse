using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class MovieConfiguration : IEntityTypeConfiguration<Movie>
{
    public void Configure(EntityTypeBuilder<Movie> builder)
    {
        builder.ToTable("Movies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(x => x.OriginalTitle)
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

        builder.Property(x => x.ImdbId)
            .HasMaxLength(40);

        builder.Property(x => x.GenresText)
            .HasMaxLength(1000);

        builder.Property(x => x.AiTagsText)
            .HasMaxLength(1000);

        builder.Property(x => x.EmotionTagsText)
            .HasMaxLength(1000);

        builder.Property(x => x.SceneTagsText)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.Title);
        builder.HasIndex(x => x.ReleaseYear);
        builder.HasIndex(x => x.TmdbId);
        builder.HasIndex(x => x.ImdbId);
        builder.HasIndex(x => x.DefaultMediaFileId);
        builder.HasIndex(x => x.AutoWatchedBaselineAtUtc);

        builder.HasMany(x => x.RatingSources)
            .WithOne(x => x.Movie)
            .HasForeignKey(x => x.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.WatchHistories)
            .WithOne(x => x.Movie)
            .HasForeignKey(x => x.MovieId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
