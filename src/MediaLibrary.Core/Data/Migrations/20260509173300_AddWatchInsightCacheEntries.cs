using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchInsightCacheEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WatchInsightCacheEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ScopeKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "TEXT", maxLength: 1600, nullable: false),
                    RefreshedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsStale = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LastAutoRefreshAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastManualRefreshAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchInsightCacheEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WatchInsightCacheEntries_ExpiresAtUtc",
                table: "WatchInsightCacheEntries",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WatchInsightCacheEntries_IsStale",
                table: "WatchInsightCacheEntries",
                column: "IsStale");

            migrationBuilder.CreateIndex(
                name: "IX_WatchInsightCacheEntries_Kind",
                table: "WatchInsightCacheEntries",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_WatchInsightCacheEntries_Kind_ScopeKey",
                table: "WatchInsightCacheEntries",
                columns: new[] { "Kind", "ScopeKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WatchInsightCacheEntries");
        }
    }
}
