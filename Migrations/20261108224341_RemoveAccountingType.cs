using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAccountingType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LessonAccountingType",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "LessonAccountingType",
                table: "DefaultSchedule");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LessonAccountingType",
                table: "Schedule",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LessonAccountingType",
                table: "DefaultSchedule",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
