using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMovieNotInterested : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsNotInterested",
                table: "UserMovieCollectionItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_UserMovieCollectionItems_IsNotInterested",
                table: "UserMovieCollectionItems",
                column: "IsNotInterested");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserMovieCollectionItems_IsNotInterested",
                table: "UserMovieCollectionItems");

            migrationBuilder.DropColumn(
                name: "IsNotInterested",
                table: "UserMovieCollectionItems");
        }
    }
}
