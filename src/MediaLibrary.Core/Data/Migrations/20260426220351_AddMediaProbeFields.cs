using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaLibrary.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaProbeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AudioBitrateKbps",
                table: "MediaFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AudioChannels",
                table: "MediaFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudioCodec",
                table: "MediaFiles",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AudioSampleRate",
                table: "MediaFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaProbeAttemptCount",
                table: "MediaFiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MediaProbeError",
                table: "MediaFiles",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MediaProbeFileSize",
                table: "MediaFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MediaProbeLastModifiedAt",
                table: "MediaFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaProbeStatus",
                table: "MediaFiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "MediaProbedAt",
                table: "MediaFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OverallBitrateKbps",
                table: "MediaFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VideoBitrateKbps",
                table: "MediaFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoCodec",
                table: "MediaFiles",
                type: "TEXT",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioBitrateKbps",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "AudioChannels",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "AudioCodec",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "AudioSampleRate",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "MediaProbeAttemptCount",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "MediaProbeError",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "MediaProbeFileSize",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "MediaProbeLastModifiedAt",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "MediaProbeStatus",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "MediaProbedAt",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "OverallBitrateKbps",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "VideoBitrateKbps",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "VideoCodec",
                table: "MediaFiles");
        }
    }
}
