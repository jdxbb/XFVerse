using MediaLibrary.Core.Data.Configurations;
using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<SourceConnection> SourceConnections => Set<SourceConnection>();

    public DbSet<ApplicationSetting> ApplicationSettings => Set<ApplicationSetting>();

    public DbSet<ScanPath> ScanPaths => Set<ScanPath>();

    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

    public DbSet<Movie> Movies => Set<Movie>();

    public DbSet<TvSeries> TvSeries => Set<TvSeries>();

    public DbSet<TvSeason> TvSeasons => Set<TvSeason>();

    public DbSet<TvEpisode> TvEpisodes => Set<TvEpisode>();

    public DbSet<RatingSource> RatingSources => Set<RatingSource>();

    public DbSet<SubtitleBinding> SubtitleBindings => Set<SubtitleBinding>();

    public DbSet<OnlineSubtitleBinding> OnlineSubtitleBindings => Set<OnlineSubtitleBinding>();

    public DbSet<WatchHistory> WatchHistories => Set<WatchHistory>();

    public DbSet<ScanTaskLog> ScanTaskLogs => Set<ScanTaskLog>();

    public DbSet<UserMovieCollectionItem> UserMovieCollectionItems => Set<UserMovieCollectionItem>();

    public DbSet<UserTvSeasonCollectionItem> UserTvSeasonCollectionItems => Set<UserTvSeasonCollectionItem>();

    public DbSet<ExternalMetadataCache> ExternalMetadataCaches => Set<ExternalMetadataCache>();

    public DbSet<WatchInsightCacheEntry> WatchInsightCacheEntries => Set<WatchInsightCacheEntry>();

    public DbSet<UserMovieStateChangeHistory> UserMovieStateChangeHistories => Set<UserMovieStateChangeHistory>();

    public DbSet<UserTvSeasonStateChangeHistory> UserTvSeasonStateChangeHistories => Set<UserTvSeasonStateChangeHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new SourceConnectionConfiguration());
        modelBuilder.ApplyConfiguration(new ApplicationSettingConfiguration());
        modelBuilder.ApplyConfiguration(new ScanPathConfiguration());
        modelBuilder.ApplyConfiguration(new MediaFileConfiguration());
        modelBuilder.ApplyConfiguration(new MovieConfiguration());
        modelBuilder.ApplyConfiguration(new TvSeriesConfiguration());
        modelBuilder.ApplyConfiguration(new TvSeasonConfiguration());
        modelBuilder.ApplyConfiguration(new TvEpisodeConfiguration());
        modelBuilder.ApplyConfiguration(new RatingSourceConfiguration());
        modelBuilder.ApplyConfiguration(new SubtitleBindingConfiguration());
        modelBuilder.ApplyConfiguration(new OnlineSubtitleBindingConfiguration());
        modelBuilder.ApplyConfiguration(new WatchHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new ScanTaskLogConfiguration());
        modelBuilder.ApplyConfiguration(new UserMovieCollectionItemConfiguration());
        modelBuilder.ApplyConfiguration(new UserTvSeasonCollectionItemConfiguration());
        modelBuilder.ApplyConfiguration(new ExternalMetadataCacheConfiguration());
        modelBuilder.ApplyConfiguration(new WatchInsightCacheEntryConfiguration());
        modelBuilder.ApplyConfiguration(new UserMovieStateChangeHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new UserTvSeasonStateChangeHistoryConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
