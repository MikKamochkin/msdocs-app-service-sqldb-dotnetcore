using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class AddZoomMeetingsTableScheduleIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ZoomMeetings_Schedule_ScheduleId",
                table: "ZoomMeetings");

            migrationBuilder.AlterColumn<Guid>(
                name: "ScheduleId",
                table: "ZoomMeetings",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddForeignKey(
                name: "FK_ZoomMeetings_Schedule_ScheduleId",
                table: "ZoomMeetings",
                column: "ScheduleId",
                principalTable: "Schedule",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ZoomMeetings_Schedule_ScheduleId",
                table: "ZoomMeetings");

            migrationBuilder.AlterColumn<Guid>(
                name: "ScheduleId",
                table: "ZoomMeetings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ZoomMeetings_Schedule_ScheduleId",
                table: "ZoomMeetings",
                column: "ScheduleId",
                principalTable: "Schedule",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
