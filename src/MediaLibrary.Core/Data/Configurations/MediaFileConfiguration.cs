using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class MediaFileConfiguration : IEntityTypeConfiguration<MediaFile>
{
    public void Configure(EntityTypeBuilder<MediaFile> builder)
    {
        builder.ToTable("MediaFiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.FilePath)
            .IsRequired()
            .HasMaxLength(1200);

        builder.Property(x => x.RemoteUri)
            .HasMaxLength(1600);

        builder.Property(x => x.Extension)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.HashValue)
            .HasMaxLength(256);

        builder.Property(x => x.CodecInfo)
            .HasMaxLength(256);

        builder.Property(x => x.VideoCodec)
            .HasMaxLength(80);

        builder.Property(x => x.AudioCodec)
            .HasMaxLength(80);

        builder.Property(x => x.MediaProbeStatus)
            .HasDefaultValue(MediaProbeStatus.NotProbed);

        builder.Property(x => x.MediaProbeError)
            .HasMaxLength(500);

        builder.Property(x => x.MediaProbeAttemptCount)
            .HasDefaultValue(0);

        builder.HasIndex(x => new { x.SourceConnectionId, x.FilePath })
            .IsUnique();

        builder.HasIndex(x => x.FileName);
        builder.HasIndex(x => x.MediaType);
        builder.HasIndex(x => x.MovieId);

        builder.HasOne(x => x.Movie)
            .WithMany(x => x.MediaFiles)
            .HasForeignKey(x => x.MovieId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.DefaultForMovie)
            .WithOne(x => x.DefaultMediaFile)
            .HasForeignKey<Movie>(x => x.DefaultMediaFileId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.WatchHistories)
            .WithOne(x => x.MediaFile)
            .HasForeignKey(x => x.MediaFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.SubtitleBindings)
            .WithOne(x => x.MediaFile)
            .HasForeignKey(x => x.MediaFileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.SubtitleBindingsAsSubtitle)
            .WithOne(x => x.SubtitleMediaFile)
            .HasForeignKey(x => x.SubtitleMediaFileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
