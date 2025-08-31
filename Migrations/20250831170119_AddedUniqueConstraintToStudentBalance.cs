using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class AddedUniqueConstraintToStudentBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentBalance_StudentId",
                table: "StudentBalance");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBalance_StudentId_AssignmentId",
                table: "StudentBalance",
                columns: new[] { "StudentId", "AssignmentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentBalance_StudentId_AssignmentId",
                table: "StudentBalance");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBalance_StudentId",
                table: "StudentBalance",
                column: "StudentId");
        }
    }
}
