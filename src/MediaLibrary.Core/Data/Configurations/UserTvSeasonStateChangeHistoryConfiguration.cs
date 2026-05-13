using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class UserTvSeasonStateChangeHistoryConfiguration : IEntityTypeConfiguration<UserTvSeasonStateChangeHistory>
{
    public void Configure(EntityTypeBuilder<UserTvSeasonStateChangeHistory> builder)
    {
        builder.ToTable("UserTvSeasonStateChangeHistories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SeriesTitle)
            .HasMaxLength(300);

        builder.Property(x => x.SeasonTitle)
            .HasMaxLength(300);

        builder.Property(x => x.StateType)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(x => x.Source)
            .IsRequired()
            .HasMaxLength(40);

        builder.HasIndex(x => new { x.StateType, x.ChangedAtUtc });
        builder.HasIndex(x => new { x.TvSeasonId, x.StateType, x.ChangedAtUtc });
        builder.HasIndex(x => new { x.TmdbSeriesId, x.SeasonNumber, x.StateType, x.ChangedAtUtc });
        builder.HasIndex(x => x.ChangedAtUtc);
    }
}
