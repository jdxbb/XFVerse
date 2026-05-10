using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class SubtitleBindingConfiguration : IEntityTypeConfiguration<SubtitleBinding>
{
    public void Configure(EntityTypeBuilder<SubtitleBinding> builder)
    {
        builder.ToTable("SubtitleBindings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Language)
            .HasMaxLength(50);

        builder.HasIndex(x => new { x.MediaFileId, x.SubtitleMediaFileId })
            .IsUnique();
    }
}
