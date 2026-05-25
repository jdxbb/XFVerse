using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class ApplicationSettingConfiguration : IEntityTypeConfiguration<ApplicationSetting>
{
    public void Configure(EntityTypeBuilder<ApplicationSetting> builder)
    {
        builder.ToTable("ApplicationSettings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TmdbReadAccessToken)
            .HasMaxLength(2048);

        builder.Property(x => x.TmdbApiKey)
            .HasMaxLength(256);

        builder.Property(x => x.OmdbApiKey)
            .HasMaxLength(256);

        builder.Property(x => x.ThemeMode)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.AiBaseUrl)
            .HasMaxLength(512);

        builder.Property(x => x.AiApiKey)
            .HasMaxLength(2048);

        builder.Property(x => x.AiModel)
            .HasMaxLength(128);

        builder.Property(x => x.RecentAiRecommendationsJson)
            .HasMaxLength(12000);

        builder.Property(x => x.CurrentAiRecommendationsJson)
            .HasMaxLength(30000);

        builder.Property(x => x.AiRecommendationLibraryFingerprint)
            .HasMaxLength(256);

        builder.Property(x => x.TmdbBaseUrl)
            .HasMaxLength(512);

        builder.Property(x => x.OpenSubtitlesEndpoint)
            .HasMaxLength(512);

        builder.Property(x => x.OpenSubtitlesApiKey)
            .HasMaxLength(512);

        builder.Property(x => x.OpenSubtitlesUsername)
            .HasMaxLength(256);

        builder.Property(x => x.OpenSubtitlesPasswordEncrypted)
            .HasMaxLength(2048);

        builder.Property(x => x.OpenSubtitlesTokenEncrypted)
            .HasMaxLength(4096);

        builder.Property(x => x.OpenSubtitlesDefaultLanguageCode)
            .HasMaxLength(32);
    }
}
