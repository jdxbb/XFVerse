using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class UserMovieCollectionItemConfiguration : IEntityTypeConfiguration<UserMovieCollectionItem>
{
    public void Configure(EntityTypeBuilder<UserMovieCollectionItem> builder)
    {
        builder.ToTable("UserMovieCollectionItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.OriginalTitle)
            .HasMaxLength(256);

        builder.Property(x => x.PosterRemoteUrl)
            .HasMaxLength(1024);

        builder.Property(x => x.Overview)
            .HasMaxLength(4000);

        builder.Property(x => x.GenresText)
            .HasMaxLength(512);

        builder.Property(x => x.Country)
            .HasMaxLength(128);

        builder.Property(x => x.Language)
            .HasMaxLength(64);

        builder.Property(x => x.ImdbId)
            .HasMaxLength(64);

        builder.Property(x => x.OmdbSourceUrl)
            .HasMaxLength(1024);

        builder.Property(x => x.LibraryVisibilityState)
            .HasConversion<int>()
            .HasDefaultValue(LibraryVisibilityState.Auto);

        builder.HasOne<Movie>()
            .WithMany()
            .HasForeignKey(x => x.MovieId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.MovieId);
        builder.HasIndex(x => x.TmdbId);
        builder.HasIndex(x => x.IsWantToWatch);
        builder.HasIndex(x => x.IsNotInterested);
        builder.HasIndex(x => x.UpdatedAt);
    }
}
