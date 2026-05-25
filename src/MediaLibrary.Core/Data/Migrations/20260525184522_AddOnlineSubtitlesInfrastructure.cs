using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineSubtitlesInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOpenSubtitlesEnabled",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OpenSubtitlesApiKey",
                table: "ApplicationSettings",
                type: "TEXT",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OpenSubtitlesDefaultLanguageCode",
                table: "ApplicationSettings",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OpenSubtitlesEndpoint",
                table: "ApplicationSettings",
                type: "TEXT",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OpenSubtitlesPasswordEncrypted",
                table: "ApplicationSettings",
                type: "TEXT",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OpenSubtitlesTokenEncrypted",
                table: "ApplicationSettings",
                type: "TEXT",
                maxLength: 4096,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OpenSubtitlesUsername",
                table: "ApplicationSettings",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "OnlineSubtitleBindings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    EpisodeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ProviderSubtitleId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProviderFileId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    LanguageCode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    LanguageName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ReleaseName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    CacheRelativePath = table.Column<string>(type: "TEXT", maxLength: 800, nullable: false),
                    CacheHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Format = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Extension = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    DownloadCount = table.Column<int>(type: "INTEGER", nullable: true),
                    Rating = table.Column<double>(type: "REAL", nullable: true),
                    Votes = table.Column<int>(type: "INTEGER", nullable: true),
                    IsHearingImpaired = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsMachineTranslated = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsAiTranslated = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsTrustedUploader = table.Column<bool>(type: "INTEGER", nullable: false),
                    Fps = table.Column<double>(type: "REAL", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnlineSubtitleBindings", x => x.Id);
                    table.CheckConstraint("CK_OnlineSubtitleBindings_Target", "((MovieId IS NOT NULL AND EpisodeId IS NULL) OR (MovieId IS NULL AND EpisodeId IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_OnlineSubtitleBindings_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OnlineSubtitleBindings_TvEpisodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "TvEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnlineSubtitleBindings_CacheHash",
                table: "OnlineSubtitleBindings",
                column: "CacheHash");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineSubtitleBindings_EpisodeId_Provider_ProviderFileId_IsDeleted",
                table: "OnlineSubtitleBindings",
                columns: new[] { "EpisodeId", "Provider", "ProviderFileId", "IsDeleted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnlineSubtitleBindings_IsDeleted",
                table: "OnlineSubtitleBindings",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineSubtitleBindings_LastUsedAt",
                table: "OnlineSubtitleBindings",
                column: "LastUsedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineSubtitleBindings_MovieId_Provider_ProviderFileId_IsDeleted",
                table: "OnlineSubtitleBindings",
                columns: new[] { "MovieId", "Provider", "ProviderFileId", "IsDeleted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnlineSubtitleBindings_ProviderSubtitleId",
                table: "OnlineSubtitleBindings",
                column: "ProviderSubtitleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnlineSubtitleBindings");

            migrationBuilder.DropColumn(
                name: "IsOpenSubtitlesEnabled",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "OpenSubtitlesApiKey",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "OpenSubtitlesDefaultLanguageCode",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "OpenSubtitlesEndpoint",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "OpenSubtitlesPasswordEncrypted",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "OpenSubtitlesTokenEncrypted",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "OpenSubtitlesUsername",
                table: "ApplicationSettings");
        }
    }
}
