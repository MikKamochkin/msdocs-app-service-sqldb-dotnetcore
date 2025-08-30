using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class AddedFieldsToZoomMeetingLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Duration",
                table: "ZoomMeetingLog",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "ZoomMeetingLog",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JoinUrl",
                table: "ZoomMeetingLog",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartTime",
                table: "ZoomMeetingLog",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZoomId",
                table: "ZoomMeetingLog",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ZoomMeetingId",
                table: "ZoomMeetingLog",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZoomMeetingLog_ZoomMeetingId",
                table: "ZoomMeetingLog",
                column: "ZoomMeetingId");

            migrationBuilder.AddForeignKey(
                name: "FK_ZoomMeetingLog_ZoomMeetings_ZoomMeetingId",
                table: "ZoomMeetingLog",
                column: "ZoomMeetingId",
                principalTable: "ZoomMeetings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ZoomMeetingLog_ZoomMeetings_ZoomMeetingId",
                table: "ZoomMeetingLog");

            migrationBuilder.DropIndex(
                name: "IX_ZoomMeetingLog_ZoomMeetingId",
                table: "ZoomMeetingLog");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "ZoomMeetingLog");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "ZoomMeetingLog");

            migrationBuilder.DropColumn(
                name: "JoinUrl",
                table: "ZoomMeetingLog");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "ZoomMeetingLog");

            migrationBuilder.DropColumn(
                name: "ZoomId",
                table: "ZoomMeetingLog");

            migrationBuilder.DropColumn(
                name: "ZoomMeetingId",
                table: "ZoomMeetingLog");
        }
    }
}
