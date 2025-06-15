using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class AddedSecondMeetingToZoomMeetings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Duration2",
                table: "ZoomMeetings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBusy2",
                table: "ZoomMeetings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "JoinUrl2",
                table: "ZoomMeetings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingId2",
                table: "ZoomMeetings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingPassword2",
                table: "ZoomMeetings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScheduleId2",
                table: "ZoomMeetings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartTime2",
                table: "ZoomMeetings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UUid2",
                table: "ZoomMeetings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZoomMeetings_ScheduleId2",
                table: "ZoomMeetings",
                column: "ScheduleId2");

            migrationBuilder.AddForeignKey(
                name: "FK_ZoomMeetings_Schedule_ScheduleId2",
                table: "ZoomMeetings",
                column: "ScheduleId2",
                principalTable: "Schedule",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ZoomMeetings_Schedule_ScheduleId2",
                table: "ZoomMeetings");

            migrationBuilder.DropIndex(
                name: "IX_ZoomMeetings_ScheduleId2",
                table: "ZoomMeetings");

            migrationBuilder.DropColumn(
                name: "Duration2",
                table: "ZoomMeetings");

            migrationBuilder.DropColumn(
                name: "IsBusy2",
                table: "ZoomMeetings");

            migrationBuilder.DropColumn(
                name: "JoinUrl2",
                table: "ZoomMeetings");

            migrationBuilder.DropColumn(
                name: "MeetingId2",
                table: "ZoomMeetings");

            migrationBuilder.DropColumn(
                name: "MeetingPassword2",
                table: "ZoomMeetings");

            migrationBuilder.DropColumn(
                name: "ScheduleId2",
                table: "ZoomMeetings");

            migrationBuilder.DropColumn(
                name: "StartTime2",
                table: "ZoomMeetings");

            migrationBuilder.DropColumn(
                name: "UUid2",
                table: "ZoomMeetings");
        }
    }
}
