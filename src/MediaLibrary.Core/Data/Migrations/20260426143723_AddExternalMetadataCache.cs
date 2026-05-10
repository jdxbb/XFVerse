using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalMetadataCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalMetadataCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CacheType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CacheKey = table.Column<string>(type: "TEXT", maxLength: 1600, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastHitAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HitCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalMetadataCache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalMetadataCache_ExpiresAtUtc",
                table: "ExternalMetadataCache",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalMetadataCache_Provider_CacheType_CacheKey",
                table: "ExternalMetadataCache",
                columns: new[] { "Provider", "CacheType", "CacheKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalMetadataCache");
        }
    }
}
