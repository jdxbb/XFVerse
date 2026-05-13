using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTvSeasonDataModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MovieId",
                table: "WatchHistories",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "EpisodeId",
                table: "WatchHistories",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EpisodeId",
                table: "MediaFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TvSeries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbSeriesId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    OriginalName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    PosterLocalPath = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: true),
                    PosterRemoteUrl = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Language = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    FirstAirDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FirstAirYear = table.Column<int>(type: "INTEGER", nullable: true),
                    GenresText = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvSeries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserTvSeasonStateChangeHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbSeriesId = table.Column<int>(type: "INTEGER", nullable: true),
                    TmdbSeasonId = table.Column<int>(type: "INTEGER", nullable: true),
                    TvSeriesId = table.Column<int>(type: "INTEGER", nullable: true),
                    TvSeasonId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserTvSeasonCollectionItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    SeriesTitle = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    SeasonTitle = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    StateType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    OldValue = table.Column<bool>(type: "INTEGER", nullable: false),
                    NewValue = table.Column<bool>(type: "INTEGER", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTvSeasonStateChangeHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TvSeasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TvSeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbSeasonId = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    PosterLocalPath = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: true),
                    PosterRemoteUrl = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: true),
                    AirDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TmdbEpisodeCount = table.Column<int>(type: "INTEGER", nullable: true),
                    IdentifiedConfidence = table.Column<double>(type: "REAL", nullable: true),
                    IdentificationStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvSeasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TvSeasons_TvSeries_TvSeriesId",
                        column: x => x.TvSeriesId,
                        principalTable: "TvSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TvEpisodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TvSeasonId = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbEpisodeId = table.Column<int>(type: "INTEGER", nullable: true),
                    EpisodeNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    StillLocalPath = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: true),
                    StillRemoteUrl = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: true),
                    AirDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RuntimeMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    IsWatched = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastPlayedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastPlayPositionSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationWatchedSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvEpisodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TvEpisodes_TvSeasons_TvSeasonId",
                        column: x => x.TvSeasonId,
                        principalTable: "TvSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTvSeasonCollectionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TvSeasonId = table.Column<int>(type: "INTEGER", nullable: true),
                    TvSeriesId = table.Column<int>(type: "INTEGER", nullable: true),
                    TmdbSeriesId = table.Column<int>(type: "INTEGER", nullable: true),
                    TmdbSeasonId = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    SeriesTitle = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    OriginalSeriesTitle = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    SeasonTitle = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    FirstAirYear = table.Column<int>(type: "INTEGER", nullable: true),
                    AirDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PosterRemoteUrl = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: false),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: false),
                    GenresText = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsWantToWatch = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsNotInterested = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTvSeasonCollectionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTvSeasonCollectionItems_TvSeasons_TvSeasonId",
                        column: x => x.TvSeasonId,
                        principalTable: "TvSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WatchHistories_EpisodeId",
                table: "WatchHistories",
                column: "EpisodeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WatchHistories_MovieId_EpisodeId_ExactlyOne",
                table: "WatchHistories",
                sql: "(MovieId IS NOT NULL AND EpisodeId IS NULL) OR (MovieId IS NULL AND EpisodeId IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_EpisodeId",
                table: "MediaFiles",
                column: "EpisodeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MediaFiles_MovieId_EpisodeId_NotBoth",
                table: "MediaFiles",
                sql: "MovieId IS NULL OR EpisodeId IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TvEpisodes_IsWatched",
                table: "TvEpisodes",
                column: "IsWatched");

            migrationBuilder.CreateIndex(
                name: "IX_TvEpisodes_LastPlayedAt",
                table: "TvEpisodes",
                column: "LastPlayedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TvEpisodes_TmdbEpisodeId",
                table: "TvEpisodes",
                column: "TmdbEpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_TvEpisodes_TvSeasonId_EpisodeNumber",
                table: "TvEpisodes",
                columns: new[] { "TvSeasonId", "EpisodeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TvSeasons_IdentificationStatus",
                table: "TvSeasons",
                column: "IdentificationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_TvSeasons_TmdbSeasonId",
                table: "TvSeasons",
                column: "TmdbSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_TvSeasons_TvSeriesId_SeasonNumber",
                table: "TvSeasons",
                columns: new[] { "TvSeriesId", "SeasonNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TvSeries_FirstAirYear",
                table: "TvSeries",
                column: "FirstAirYear");

            migrationBuilder.CreateIndex(
                name: "IX_TvSeries_Name",
                table: "TvSeries",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_TvSeries_TmdbSeriesId",
                table: "TvSeries",
                column: "TmdbSeriesId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTvSeasonCollectionItems_IsFavorite",
                table: "UserTvSeasonCollectionItems",
                column: "IsFavorite");

            migrationBuilder.CreateIndex(
                name: "IX_UserTvSeasonCollectionItems_IsNotInterested",
                table: "UserTvSeasonCollectionItems",
                column: "IsNotInterested");

            migrationBuilder.CreateIndex(
                name: "IX_UserTvSeasonCollectionItems_IsWantToWatch",
                table: "UserTvSeasonCollectionItems",
                column: "IsWantToWatch");

            migrationBuilder.CreateIndex(
                name: "IX_UserTvSeasonCollectionItems_TmdbSeasonId",
                table: "UserTvSeasonCollectionItems",
                column: "TmdbSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTvSeasonCollectionItems_TmdbSeriesId",
                table: "UserTvSeasonCollectionItems",
                column: "TmdbSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTvSeasonCollectionItems_TmdbSeriesId_SeasonNumber",
                table: "UserTvSeasonCollectionItems",
                columns: new[] { "TmdbSeriesId", "SeasonNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTvSeasonCollectionItems_TvSeasonId",
                table: "UserTvSeasonCollectionItems",
                column: "TvSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTvSeasonCollectionItems_TvSeriesId",
                table: "UserTvSeasonCollectionItems",
                column: "TvSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTvSeasonCollectionItems_UpdatedAt",
                table: "UserTvSeasonCollectionItems",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserTvSeasonStateChangeHistories_ChangedAtUtc",
                table: "UserTvSeasonStateChangeHistories",
                column: "ChangedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UserTvSeasonStateChangeHistories_StateType_ChangedAtUtc",
                table: "UserTvSeasonStateChangeHistories",
                columns: new[] { "StateType", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTvSeasonStateChangeHistories_TmdbSeriesId_SeasonNumber_StateType_ChangedAtUtc",
                table: "UserTvSeasonStateChangeHistories",
                columns: new[] { "TmdbSeriesId", "SeasonNumber", "StateType", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTvSeasonStateChangeHistories_TvSeasonId_StateType_ChangedAtUtc",
                table: "UserTvSeasonStateChangeHistories",
                columns: new[] { "TvSeasonId", "StateType", "ChangedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_MediaFiles_TvEpisodes_EpisodeId",
                table: "MediaFiles",
                column: "EpisodeId",
                principalTable: "TvEpisodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchHistories_TvEpisodes_EpisodeId",
                table: "WatchHistories",
                column: "EpisodeId",
                principalTable: "TvEpisodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaFiles_TvEpisodes_EpisodeId",
                table: "MediaFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_WatchHistories_TvEpisodes_EpisodeId",
                table: "WatchHistories");

            migrationBuilder.DropTable(
                name: "TvEpisodes");

            migrationBuilder.DropTable(
                name: "UserTvSeasonCollectionItems");

            migrationBuilder.DropTable(
                name: "UserTvSeasonStateChangeHistories");

            migrationBuilder.DropTable(
                name: "TvSeasons");

            migrationBuilder.DropTable(
                name: "TvSeries");

            migrationBuilder.DropIndex(
                name: "IX_WatchHistories_EpisodeId",
                table: "WatchHistories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WatchHistories_MovieId_EpisodeId_ExactlyOne",
                table: "WatchHistories");

            migrationBuilder.DropIndex(
                name: "IX_MediaFiles_EpisodeId",
                table: "MediaFiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MediaFiles_MovieId_EpisodeId_NotBoth",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "EpisodeId",
                table: "WatchHistories");

            migrationBuilder.DropColumn(
                name: "EpisodeId",
                table: "MediaFiles");

            migrationBuilder.AlterColumn<int>(
                name: "MovieId",
                table: "WatchHistories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
