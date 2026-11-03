using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class StudentBalanceRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Old balances and logs are in per-assignment lesson credits, so all three tables start clean.
            // Balances are re-seeded below as one zeroed currency balance per student.
            // EmailPaymentsLog goes first because it has a no-cascade FK to StudentBalanceTransactionLog.
            migrationBuilder.Sql(@"
                DELETE FROM [EmailPaymentsLog];
                DELETE FROM [StudentBalanceTransactionLog];
                DELETE FROM [StudentBalance];");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentBalance_Assignments_AssignmentId",
                table: "StudentBalance");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentBalanceTransactionLog_Assignments_AssignmentId",
                table: "StudentBalanceTransactionLog");

            migrationBuilder.DropIndex(
                name: "IX_StudentBalanceTransactionLog_AssignmentId",
                table: "StudentBalanceTransactionLog");

            migrationBuilder.DropIndex(
                name: "IX_StudentBalance_AssignmentId",
                table: "StudentBalance");

            migrationBuilder.DropIndex(
                name: "IX_StudentBalance_StudentId_AssignmentId",
                table: "StudentBalance");

            migrationBuilder.DropColumn(
                name: "AssignmentId",
                table: "StudentBalanceTransactionLog");

            migrationBuilder.DropColumn(
                name: "AssignmentId",
                table: "StudentBalance");

            migrationBuilder.DropColumn(
                name: "Balance",
                table: "StudentBalance");

            migrationBuilder.AddColumn<float>(
                name: "USDBalance",
                table: "StudentBalance",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "CADBalance",
                table: "StudentBalance",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "EURBalance",
                table: "StudentBalance",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "RUBBalance",
                table: "StudentBalance",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.CreateIndex(
                name: "IX_StudentBalance_StudentId",
                table: "StudentBalance",
                column: "StudentId",
                unique: true);

            // One blank balance per existing student
            migrationBuilder.Sql(@"
                INSERT INTO [StudentBalance] ([Id], [StudentId], [CADBalance], [USDBalance], [EURBalance], [RUBBalance])
                SELECT NEWID(), [ID], 0, 0, 0, 0
                FROM [Student];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Per-student rows can't be mapped back to assignments, and an empty AssignmentId
            // would violate the restored FK, so the old schema comes back empty.
            migrationBuilder.Sql("DELETE FROM [StudentBalance];");

            migrationBuilder.DropIndex(
                name: "IX_StudentBalance_StudentId",
                table: "StudentBalance");

            migrationBuilder.DropColumn(
                name: "CADBalance",
                table: "StudentBalance");

            migrationBuilder.DropColumn(
                name: "EURBalance",
                table: "StudentBalance");

            migrationBuilder.DropColumn(
                name: "RUBBalance",
                table: "StudentBalance");

            migrationBuilder.DropColumn(
                name: "USDBalance",
                table: "StudentBalance");

            migrationBuilder.AddColumn<float>(
                name: "Balance",
                table: "StudentBalance",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignmentId",
                table: "StudentBalanceTransactionLog",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignmentId",
                table: "StudentBalance",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_StudentBalanceTransactionLog_AssignmentId",
                table: "StudentBalanceTransactionLog",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBalance_AssignmentId",
                table: "StudentBalance",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBalance_StudentId_AssignmentId",
                table: "StudentBalance",
                columns: new[] { "StudentId", "AssignmentId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentBalance_Assignments_AssignmentId",
                table: "StudentBalance",
                column: "AssignmentId",
                principalTable: "Assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentBalanceTransactionLog_Assignments_AssignmentId",
                table: "StudentBalanceTransactionLog",
                column: "AssignmentId",
                principalTable: "Assignments",
                principalColumn: "Id");
        }
    }
}
