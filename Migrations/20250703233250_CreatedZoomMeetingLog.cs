using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class CreatedZoomMeetingLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZoomMeetingLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoomMeetingLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZoomMeetingLog_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ZoomMeetingLog_Schedule_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedule",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZoomMeetingLog_AssignmentId",
                table: "ZoomMeetingLog",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ZoomMeetingLog_ScheduleId",
                table: "ZoomMeetingLog",
                column: "ScheduleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZoomMeetingLog");
        }
    }
}
