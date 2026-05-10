using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class UserMovieStateChangeHistoryConfiguration : IEntityTypeConfiguration<UserMovieStateChangeHistory>
{
    public void Configure(EntityTypeBuilder<UserMovieStateChangeHistory> builder)
    {
        builder.ToTable("UserMovieStateChangeHistories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(300);

        builder.Property(x => x.StateType)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(x => x.Source)
            .IsRequired()
            .HasMaxLength(40);

        builder.HasIndex(x => new { x.StateType, x.ChangedAtUtc });
        builder.HasIndex(x => new { x.TmdbId, x.StateType, x.ChangedAtUtc });
        builder.HasIndex(x => x.ChangedAtUtc);
    }
}
