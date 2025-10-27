using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class AddedHasReachedMinimumDurationBools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstMutualPresenceTime",
                table: "ZoomMeetings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasReachedMinimumDuration",
                table: "Schedule",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstMutualPresenceTime",
                table: "ZoomMeetings");

            migrationBuilder.DropColumn(
                name: "HasReachedMinimumDuration",
                table: "Schedule");
        }
    }
}
