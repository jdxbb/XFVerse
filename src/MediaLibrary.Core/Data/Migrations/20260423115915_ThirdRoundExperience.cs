using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ThirdRoundExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RemoteUri",
                table: "MediaFiles",
                type: "TEXT",
                maxLength: 1600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiApiKey",
                table: "ApplicationSettings",
                type: "TEXT",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AiBaseUrl",
                table: "ApplicationSettings",
                type: "TEXT",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AiModel",
                table: "ApplicationSettings",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ThemeMode",
                table: "ApplicationSettings",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RemoteUri",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "AiApiKey",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "AiBaseUrl",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "AiModel",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "ThemeMode",
                table: "ApplicationSettings");
        }
    }
}
