using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryVisibilityState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LibraryVisibilityState",
                table: "UserTvSeasonCollectionItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LibraryVisibilityState",
                table: "UserMovieCollectionItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LibraryVisibilityState",
                table: "UserTvSeasonCollectionItems");

            migrationBuilder.DropColumn(
                name: "LibraryVisibilityState",
                table: "UserMovieCollectionItems");
        }
    }
}
