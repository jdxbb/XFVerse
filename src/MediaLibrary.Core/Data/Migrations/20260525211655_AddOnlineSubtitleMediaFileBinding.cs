using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineSubtitleMediaFileBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_OnlineSubtitleBindings_Target",
                table: "OnlineSubtitleBindings");

            migrationBuilder.AddColumn<int>(
                name: "MediaFileId",
                table: "OnlineSubtitleBindings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnlineSubtitleBindings_MediaFileId_Provider_ProviderFileId_IsDeleted",
                table: "OnlineSubtitleBindings",
                columns: new[] { "MediaFileId", "Provider", "ProviderFileId", "IsDeleted" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_OnlineSubtitleBindings_Target",
                table: "OnlineSubtitleBindings",
                sql: "((MovieId IS NOT NULL AND EpisodeId IS NULL AND MediaFileId IS NULL) OR (MovieId IS NULL AND EpisodeId IS NOT NULL AND MediaFileId IS NULL) OR (MovieId IS NULL AND EpisodeId IS NULL AND MediaFileId IS NOT NULL))");

            migrationBuilder.AddForeignKey(
                name: "FK_OnlineSubtitleBindings_MediaFiles_MediaFileId",
                table: "OnlineSubtitleBindings",
                column: "MediaFileId",
                principalTable: "MediaFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OnlineSubtitleBindings_MediaFiles_MediaFileId",
                table: "OnlineSubtitleBindings");

            migrationBuilder.DropIndex(
                name: "IX_OnlineSubtitleBindings_MediaFileId_Provider_ProviderFileId_IsDeleted",
                table: "OnlineSubtitleBindings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OnlineSubtitleBindings_Target",
                table: "OnlineSubtitleBindings");

            migrationBuilder.DropColumn(
                name: "MediaFileId",
                table: "OnlineSubtitleBindings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OnlineSubtitleBindings_Target",
                table: "OnlineSubtitleBindings",
                sql: "((MovieId IS NOT NULL AND EpisodeId IS NULL) OR (MovieId IS NULL AND EpisodeId IS NOT NULL))");
        }
    }
}
