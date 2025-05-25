using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class ChangedZoomMeetingsToDynamicMeetings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Pmi",
                table: "ZoomMeetings",
                newName: "MeetingPassword");

            migrationBuilder.AddColumn<string>(
                name: "JoinUrl",
                table: "ZoomMeetings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MeetingId",
                table: "ZoomMeetings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JoinUrl",
                table: "ZoomMeetings");

            migrationBuilder.DropColumn(
                name: "MeetingId",
                table: "ZoomMeetings");

            migrationBuilder.RenameColumn(
                name: "MeetingPassword",
                table: "ZoomMeetings",
                newName: "Pmi");
        }
    }
}
