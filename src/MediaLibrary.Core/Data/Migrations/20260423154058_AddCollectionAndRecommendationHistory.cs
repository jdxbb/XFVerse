using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionAndRecommendationHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecentAiRecommendationsJson",
                table: "ApplicationSettings",
                type: "TEXT",
                maxLength: 12000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "UserMovieCollectionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    TmdbId = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    OriginalTitle = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ReleaseYear = table.Column<int>(type: "INTEGER", nullable: true),
                    PosterRemoteUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    GenresText = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RuntimeMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    ImdbId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsWantToWatch = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsWatched = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsInLibrary = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMovieCollectionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMovieCollectionItems_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserMovieCollectionItems_IsWantToWatch",
                table: "UserMovieCollectionItems",
                column: "IsWantToWatch");

            migrationBuilder.CreateIndex(
                name: "IX_UserMovieCollectionItems_MovieId",
                table: "UserMovieCollectionItems",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMovieCollectionItems_TmdbId",
                table: "UserMovieCollectionItems",
                column: "TmdbId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMovieCollectionItems_UpdatedAt",
                table: "UserMovieCollectionItems",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserMovieCollectionItems");

            migrationBuilder.DropColumn(
                name: "RecentAiRecommendationsJson",
                table: "ApplicationSettings");
        }
    }
}
