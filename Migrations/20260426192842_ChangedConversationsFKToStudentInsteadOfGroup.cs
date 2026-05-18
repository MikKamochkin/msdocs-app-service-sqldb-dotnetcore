using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class ChangedConversationsFKToStudentInsteadOfGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Group_GroupId",
                table: "Conversations");

            migrationBuilder.RenameColumn(
                name: "GroupId",
                table: "Conversations",
                newName: "StudentID");

            migrationBuilder.RenameIndex(
                name: "IX_Conversations_GroupId",
                table: "Conversations",
                newName: "IX_Conversations_StudentID");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Student_StudentID",
                table: "Conversations",
                column: "StudentID",
                principalTable: "Student",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Student_StudentID",
                table: "Conversations");

            migrationBuilder.RenameColumn(
                name: "StudentID",
                table: "Conversations",
                newName: "GroupId");

            migrationBuilder.RenameIndex(
                name: "IX_Conversations_StudentID",
                table: "Conversations",
                newName: "IX_Conversations_GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Group_GroupId",
                table: "Conversations",
                column: "GroupId",
                principalTable: "Group",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
