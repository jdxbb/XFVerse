using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionRatingSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OmdbLastUpdatedAt",
                table: "UserMovieCollectionItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OmdbScoreScale",
                table: "UserMovieCollectionItems",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OmdbScoreValue",
                table: "UserMovieCollectionItems",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OmdbSourceUrl",
                table: "UserMovieCollectionItems",
                type: "TEXT",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "OmdbVoteCount",
                table: "UserMovieCollectionItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TmdbRating",
                table: "UserMovieCollectionItems",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TmdbVoteCount",
                table: "UserMovieCollectionItems",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OmdbLastUpdatedAt",
                table: "UserMovieCollectionItems");

            migrationBuilder.DropColumn(
                name: "OmdbScoreScale",
                table: "UserMovieCollectionItems");

            migrationBuilder.DropColumn(
                name: "OmdbScoreValue",
                table: "UserMovieCollectionItems");

            migrationBuilder.DropColumn(
                name: "OmdbSourceUrl",
                table: "UserMovieCollectionItems");

            migrationBuilder.DropColumn(
                name: "OmdbVoteCount",
                table: "UserMovieCollectionItems");

            migrationBuilder.DropColumn(
                name: "TmdbRating",
                table: "UserMovieCollectionItems");

            migrationBuilder.DropColumn(
                name: "TmdbVoteCount",
                table: "UserMovieCollectionItems");
        }
    }
}
