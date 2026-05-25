using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class OnlineSubtitleBindingConfiguration : IEntityTypeConfiguration<OnlineSubtitleBinding>
{
    public void Configure(EntityTypeBuilder<OnlineSubtitleBinding> builder)
    {
        builder.ToTable(
            "OnlineSubtitleBindings",
            table => table.HasCheckConstraint(
                "CK_OnlineSubtitleBindings_Target",
                "((MovieId IS NOT NULL AND EpisodeId IS NULL) OR (MovieId IS NULL AND EpisodeId IS NOT NULL))"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.ProviderSubtitleId)
            .HasMaxLength(128);

        builder.Property(x => x.ProviderFileId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.LanguageCode)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.LanguageName)
            .HasMaxLength(120);

        builder.Property(x => x.DisplayName)
            .HasMaxLength(500);

        builder.Property(x => x.ReleaseName)
            .HasMaxLength(500);

        builder.Property(x => x.FileName)
            .HasMaxLength(260);

        builder.Property(x => x.CacheRelativePath)
            .IsRequired()
            .HasMaxLength(800);

        builder.Property(x => x.CacheHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.Format)
            .HasMaxLength(32);

        builder.Property(x => x.Extension)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.MetadataJson)
            .HasMaxLength(8000);

        builder.HasIndex(x => new { x.MovieId, x.Provider, x.ProviderFileId, x.IsDeleted })
            .IsUnique();

        builder.HasIndex(x => new { x.EpisodeId, x.Provider, x.ProviderFileId, x.IsDeleted })
            .IsUnique();

        builder.HasIndex(x => x.ProviderSubtitleId);
        builder.HasIndex(x => x.CacheHash);
        builder.HasIndex(x => x.IsDeleted);
        builder.HasIndex(x => x.LastUsedAt);

        builder.HasOne(x => x.Movie)
            .WithMany()
            .HasForeignKey(x => x.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Episode)
            .WithMany()
            .HasForeignKey(x => x.EpisodeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
