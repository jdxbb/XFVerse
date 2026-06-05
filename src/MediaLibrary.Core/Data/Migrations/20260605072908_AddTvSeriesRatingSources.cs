using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTvSeriesRatingSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TvSeriesRatingSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TvSeriesId = table.Column<int>(type: "INTEGER", nullable: false),
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
                    table.PrimaryKey("PK_TvSeriesRatingSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TvSeriesRatingSources_TvSeries_TvSeriesId",
                        column: x => x.TvSeriesId,
                        principalTable: "TvSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TvSeriesRatingSources_TvSeriesId_SourceName",
                table: "TvSeriesRatingSources",
                columns: new[] { "TvSeriesId", "SourceName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TvSeriesRatingSources");
        }
    }
}
