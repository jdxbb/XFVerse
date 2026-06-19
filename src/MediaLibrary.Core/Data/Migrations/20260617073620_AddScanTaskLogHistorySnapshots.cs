using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScanTaskLogHistorySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScanPathDisplayNameSnapshot",
                table: "ScanTaskLogs",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScanPathSnapshot",
                table: "ScanTaskLogs",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceBaseUrlSnapshot",
                table: "ScanTaskLogs",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUsernameSnapshot",
                table: "ScanTaskLogs",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE ScanTaskLogs
                SET
                    SourceBaseUrlSnapshot = COALESCE(
                        CASE
                            WHEN ReasonSummaryJson IS NOT NULL AND json_valid(ReasonSummaryJson)
                            THEN json_extract(ReasonSummaryJson, '$.snapshot.baseUrl')
                        END,
                        (SELECT SourceConnections.BaseUrl
                         FROM SourceConnections
                         WHERE SourceConnections.Id = ScanTaskLogs.SourceConnectionId)
                    ),
                    SourceUsernameSnapshot = COALESCE(
                        CASE
                            WHEN ReasonSummaryJson IS NOT NULL AND json_valid(ReasonSummaryJson)
                            THEN json_extract(ReasonSummaryJson, '$.snapshot.username')
                        END,
                        (SELECT SourceConnections.Username
                         FROM SourceConnections
                         WHERE SourceConnections.Id = ScanTaskLogs.SourceConnectionId)
                    ),
                    ScanPathSnapshot = COALESCE(
                        CASE
                            WHEN ReasonSummaryJson IS NOT NULL AND json_valid(ReasonSummaryJson)
                            THEN json_extract(ReasonSummaryJson, '$.snapshot.scanPath')
                        END,
                        (SELECT ScanPaths.Path
                         FROM ScanPaths
                         WHERE ScanPaths.Id = ScanTaskLogs.ScanPathId)
                    ),
                    ScanPathDisplayNameSnapshot = COALESCE(
                        CASE
                            WHEN ReasonSummaryJson IS NOT NULL AND json_valid(ReasonSummaryJson)
                            THEN json_extract(ReasonSummaryJson, '$.snapshot.scanPathDisplayName')
                        END,
                        (SELECT ScanPaths.DisplayName
                         FROM ScanPaths
                         WHERE ScanPaths.Id = ScanTaskLogs.ScanPathId)
                    )
                WHERE SourceBaseUrlSnapshot IS NULL
                   OR SourceUsernameSnapshot IS NULL
                   OR ScanPathSnapshot IS NULL
                   OR ScanPathDisplayNameSnapshot IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScanPathDisplayNameSnapshot",
                table: "ScanTaskLogs");

            migrationBuilder.DropColumn(
                name: "ScanPathSnapshot",
                table: "ScanTaskLogs");

            migrationBuilder.DropColumn(
                name: "SourceBaseUrlSnapshot",
                table: "ScanTaskLogs");

            migrationBuilder.DropColumn(
                name: "SourceUsernameSnapshot",
                table: "ScanTaskLogs");
        }
    }
}
