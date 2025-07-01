using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class StudentBalanceStuff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StudentUnitBalance",
                table: "Assignments");

            migrationBuilder.CreateTable(
                name: "Payer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payer_Student_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Student",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentBalance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Balance = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentBalance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentBalance_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentBalance_Student_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Student",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentBalanceTransactionLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentBalance = table.Column<float>(type: "real", nullable: true),
                    TransactionAmount = table.Column<float>(type: "real", nullable: true),
                    AmountPaid = table.Column<float>(type: "real", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PayerNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdminNotes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentBalanceTransactionLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentBalanceTransactionLog_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentBalanceTransactionLog_Student_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Student",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payer_StudentId",
                table: "Payer",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBalance_AssignmentId",
                table: "StudentBalance",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBalance_StudentId",
                table: "StudentBalance",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBalanceTransactionLog_AssignmentId",
                table: "StudentBalanceTransactionLog",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBalanceTransactionLog_StudentId",
                table: "StudentBalanceTransactionLog",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payer");

            migrationBuilder.DropTable(
                name: "StudentBalance");

            migrationBuilder.DropTable(
                name: "StudentBalanceTransactionLog");

            migrationBuilder.AddColumn<float>(
                name: "StudentUnitBalance",
                table: "Assignments",
                type: "real",
                nullable: false,
                defaultValue: 0f);
        }
    }
}
