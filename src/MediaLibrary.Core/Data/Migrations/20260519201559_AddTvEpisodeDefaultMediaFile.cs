using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTvEpisodeDefaultMediaFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultMediaFileId",
                table: "TvEpisodes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TvEpisodes_DefaultMediaFileId",
                table: "TvEpisodes",
                column: "DefaultMediaFileId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TvEpisodes_MediaFiles_DefaultMediaFileId",
                table: "TvEpisodes",
                column: "DefaultMediaFileId",
                principalTable: "MediaFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TvEpisodes_MediaFiles_DefaultMediaFileId",
                table: "TvEpisodes");

            migrationBuilder.DropIndex(
                name: "IX_TvEpisodes_DefaultMediaFileId",
                table: "TvEpisodes");

            migrationBuilder.DropColumn(
                name: "DefaultMediaFileId",
                table: "TvEpisodes");
        }
    }
}
