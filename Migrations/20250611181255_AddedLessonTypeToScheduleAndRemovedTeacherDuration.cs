using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class AddedLessonTypeToScheduleAndRemovedTeacherDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeacherDuration",
                table: "Schedule");

            migrationBuilder.AddColumn<string>(
                name: "LessonType",
                table: "Schedule",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LessonType",
                table: "Schedule");

            migrationBuilder.AddColumn<int>(
                name: "TeacherDuration",
                table: "Schedule",
                type: "int",
                nullable: true);
        }
    }
}
