using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMovieCollectionFavorite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "UserMovieCollectionItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_UserMovieCollectionItems_IsFavorite",
                table: "UserMovieCollectionItems",
                column: "IsFavorite");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserMovieCollectionItems_IsFavorite",
                table: "UserMovieCollectionItems");

            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "UserMovieCollectionItems");
        }
    }
}
