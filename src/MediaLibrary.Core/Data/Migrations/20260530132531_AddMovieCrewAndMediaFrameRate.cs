using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMovieCrewAndMediaFrameRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorsText",
                table: "Movies",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DirectorText",
                table: "Movies",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionCompanyText",
                table: "Movies",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WriterText",
                table: "Movies",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VideoFrameRate",
                table: "MediaFiles",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActorsText",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "DirectorText",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "ProductionCompanyText",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "WriterText",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "VideoFrameRate",
                table: "MediaFiles");
        }
    }
}
