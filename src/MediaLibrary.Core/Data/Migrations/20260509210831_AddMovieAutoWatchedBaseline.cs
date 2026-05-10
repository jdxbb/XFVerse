using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMovieAutoWatchedBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AutoWatchedBaselineAtUtc",
                table: "Movies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movies_AutoWatchedBaselineAtUtc",
                table: "Movies",
                column: "AutoWatchedBaselineAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Movies_AutoWatchedBaselineAtUtc",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "AutoWatchedBaselineAtUtc",
                table: "Movies");
        }
    }
}
