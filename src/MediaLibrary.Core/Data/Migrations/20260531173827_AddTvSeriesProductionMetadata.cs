using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTvSeriesProductionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NetworksText",
                table: "TvSeries",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionCompaniesText",
                table: "TvSeries",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionStatus",
                table: "TvSeries",
                type: "TEXT",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NetworksText",
                table: "TvSeries");

            migrationBuilder.DropColumn(
                name: "ProductionCompaniesText",
                table: "TvSeries");

            migrationBuilder.DropColumn(
                name: "ProductionStatus",
                table: "TvSeries");
        }
    }
}
