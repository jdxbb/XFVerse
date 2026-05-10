using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SourceConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ProtocolType = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PasswordEncrypted = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastConnectedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastScanAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScanPaths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceConnectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRecursive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanPaths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanPaths_SourceConnections_SourceConnectionId",
                        column: x => x.SourceConnectionId,
                        principalTable: "SourceConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScanTaskLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceConnectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ScanPathId = table.Column<int>(type: "INTEGER", nullable: true),
                    TaskType = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ScannedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    NewFileCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedFileCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IgnoredFileCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanTaskLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanTaskLogs_ScanPaths_ScanPathId",
                        column: x => x.ScanPathId,
                        principalTable: "ScanPaths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScanTaskLogs_SourceConnections_SourceConnectionId",
                        column: x => x.SourceConnectionId,
                        principalTable: "SourceConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediaFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceConnectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ScanPathId = table.Column<int>(type: "INTEGER", nullable: true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: false),
                    Extension = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MediaType = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    ResolutionWidth = table.Column<int>(type: "INTEGER", nullable: true),
                    ResolutionHeight = table.Column<int>(type: "INTEGER", nullable: true),
                    HashValue = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CodecInfo = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaFiles_ScanPaths_ScanPathId",
                        column: x => x.ScanPathId,
                        principalTable: "ScanPaths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MediaFiles_SourceConnections_SourceConnectionId",
                        column: x => x.SourceConnectionId,
                        principalTable: "SourceConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Movies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    OriginalTitle = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ReleaseYear = table.Column<int>(type: "INTEGER", nullable: true),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    PosterLocalPath = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: true),
                    PosterRemoteUrl = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Language = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    RuntimeMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    TmdbId = table.Column<int>(type: "INTEGER", nullable: true),
                    ImdbId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    IdentifiedConfidence = table.Column<double>(type: "REAL", nullable: true),
                    IdentificationStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    GenresText = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    AiTagsText = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    EmotionTagsText = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    SceneTagsText = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsWatched = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserRating = table.Column<double>(type: "REAL", nullable: true),
                    LastPlayedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DefaultMediaFileId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Movies_MediaFiles_DefaultMediaFileId",
                        column: x => x.DefaultMediaFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SubtitleBindings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediaFileId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubtitleMediaFileId = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchType = table.Column<int>(type: "INTEGER", nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsAutoLoaded = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubtitleBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubtitleBindings_MediaFiles_MediaFileId",
                        column: x => x.MediaFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubtitleBindings_MediaFiles_SubtitleMediaFileId",
                        column: x => x.SubtitleMediaFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RatingSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ScoreValue = table.Column<double>(type: "REAL", nullable: false),
                    ScoreScale = table.Column<double>(type: "REAL", nullable: false),
                    VoteCount = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: true),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatingSources_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WatchHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaFileId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastPlayPositionSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationWatchedSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchHistories_MediaFiles_MediaFileId",
                        column: x => x.MediaFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WatchHistories_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_FileName",
                table: "MediaFiles",
                column: "FileName");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_MediaType",
                table: "MediaFiles",
                column: "MediaType");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_MovieId",
                table: "MediaFiles",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_ScanPathId",
                table: "MediaFiles",
                column: "ScanPathId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_SourceConnectionId_FilePath",
                table: "MediaFiles",
                columns: new[] { "SourceConnectionId", "FilePath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movies_DefaultMediaFileId",
                table: "Movies",
                column: "DefaultMediaFileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movies_ImdbId",
                table: "Movies",
                column: "ImdbId");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_ReleaseYear",
                table: "Movies",
                column: "ReleaseYear");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_Title",
                table: "Movies",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_TmdbId",
                table: "Movies",
                column: "TmdbId");

            migrationBuilder.CreateIndex(
                name: "IX_RatingSources_MovieId_SourceName",
                table: "RatingSources",
                columns: new[] { "MovieId", "SourceName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScanPaths_SourceConnectionId_Path",
                table: "ScanPaths",
                columns: new[] { "SourceConnectionId", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScanTaskLogs_CreatedAt",
                table: "ScanTaskLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScanTaskLogs_ScanPathId",
                table: "ScanTaskLogs",
                column: "ScanPathId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanTaskLogs_SourceConnectionId",
                table: "ScanTaskLogs",
                column: "SourceConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubtitleBindings_MediaFileId_SubtitleMediaFileId",
                table: "SubtitleBindings",
                columns: new[] { "MediaFileId", "SubtitleMediaFileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubtitleBindings_SubtitleMediaFileId",
                table: "SubtitleBindings",
                column: "SubtitleMediaFileId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchHistories_CreatedAt",
                table: "WatchHistories",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WatchHistories_MediaFileId",
                table: "WatchHistories",
                column: "MediaFileId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchHistories_MovieId",
                table: "WatchHistories",
                column: "MovieId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaFiles_Movies_MovieId",
                table: "MediaFiles",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaFiles_Movies_MovieId",
                table: "MediaFiles");

            migrationBuilder.DropTable(
                name: "RatingSources");

            migrationBuilder.DropTable(
                name: "ScanTaskLogs");

            migrationBuilder.DropTable(
                name: "SubtitleBindings");

            migrationBuilder.DropTable(
                name: "WatchHistories");

            migrationBuilder.DropTable(
                name: "Movies");

            migrationBuilder.DropTable(
                name: "MediaFiles");

            migrationBuilder.DropTable(
                name: "ScanPaths");

            migrationBuilder.DropTable(
                name: "SourceConnections");
        }
    }
}
