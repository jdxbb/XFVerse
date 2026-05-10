using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommendationCacheState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiRecommendationLibraryFingerprint",
                table: "ApplicationSettings",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CurrentAiRecommendationsJson",
                table: "ApplicationSettings",
                type: "TEXT",
                maxLength: 30000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiRecommendationLibraryFingerprint",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "CurrentAiRecommendationsJson",
                table: "ApplicationSettings");
        }
    }
}
