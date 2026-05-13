using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaLibrary.Core.Data.Configurations;

public sealed class WatchHistoryConfiguration : IEntityTypeConfiguration<WatchHistory>
{
    public void Configure(EntityTypeBuilder<WatchHistory> builder)
    {
        builder.ToTable(
            "WatchHistories",
            table => table.HasCheckConstraint(
                "CK_WatchHistories_MovieId_EpisodeId_ExactlyOne",
                "(MovieId IS NOT NULL AND EpisodeId IS NULL) OR (MovieId IS NULL AND EpisodeId IS NOT NULL)"));

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.MovieId);
        builder.HasIndex(x => x.EpisodeId);
    }
}
