using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeZoneToTeacher2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Teacher",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Teacher");
        }
    }
}
