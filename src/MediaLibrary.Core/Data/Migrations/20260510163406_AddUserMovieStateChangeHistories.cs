using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMovieStateChangeHistories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserMovieStateChangeHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbId = table.Column<int>(type: "INTEGER", nullable: false),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserMovieCollectionItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    StateType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    OldValue = table.Column<bool>(type: "INTEGER", nullable: false),
                    NewValue = table.Column<bool>(type: "INTEGER", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMovieStateChangeHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserMovieStateChangeHistories_ChangedAtUtc",
                table: "UserMovieStateChangeHistories",
                column: "ChangedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UserMovieStateChangeHistories_StateType_ChangedAtUtc",
                table: "UserMovieStateChangeHistories",
                columns: new[] { "StateType", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMovieStateChangeHistories_TmdbId_StateType_ChangedAtUtc",
                table: "UserMovieStateChangeHistories",
                columns: new[] { "TmdbId", "StateType", "ChangedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserMovieStateChangeHistories");
        }
    }
}
