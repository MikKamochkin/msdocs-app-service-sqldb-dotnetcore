using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAssignmentIdFromZoomMeetingLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ZoomMeetingLog_Assignments_AssignmentId",
                table: "ZoomMeetingLog");

            migrationBuilder.DropIndex(
                name: "IX_ZoomMeetingLog_AssignmentId",
                table: "ZoomMeetingLog");

            migrationBuilder.DropColumn(
                name: "AssignmentId",
                table: "ZoomMeetingLog");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignmentId",
                table: "ZoomMeetingLog",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZoomMeetingLog_AssignmentId",
                table: "ZoomMeetingLog",
                column: "AssignmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ZoomMeetingLog_Assignments_AssignmentId",
                table: "ZoomMeetingLog",
                column: "AssignmentId",
                principalTable: "Assignments",
                principalColumn: "Id");
        }
    }
}
