using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class AddedEmailPaymentsLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailPaymentsLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandledAutomatically = table.Column<bool>(type: "bit", nullable: true),
                    StudentBalanceTransactionLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReasonForFailure = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StudentName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PayerName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailPaymentsLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailPaymentsLog_StudentBalanceTransactionLog_StudentBalanceTransactionLogId",
                        column: x => x.StudentBalanceTransactionLogId,
                        principalTable: "StudentBalanceTransactionLog",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailPaymentsLog_StudentBalanceTransactionLogId",
                table: "EmailPaymentsLog",
                column: "StudentBalanceTransactionLogId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailPaymentsLog");
        }
    }
}
