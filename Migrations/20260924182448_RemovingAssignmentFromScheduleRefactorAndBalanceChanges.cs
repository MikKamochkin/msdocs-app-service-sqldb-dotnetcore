using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class RemovingAssignmentFromScheduleRefactorAndBalanceChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Schedule_Assignments_AssignmentId",
                table: "Schedule");

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "Schedule",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "StudentChargeAmount",
                table: "Schedule",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentChargeCurrency",
                table: "Schedule",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeacherId",
                table: "Schedule",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "TeacherPayAmount",
                table: "Schedule",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeacherPayCurrency",
                table: "Schedule",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql("""
            IF EXISTS
            (
                SELECT 1
                FROM [Schedule] AS s
                LEFT JOIN [Assignments] AS a
                    ON a.[Id] = s.[AssignmentId]
                LEFT JOIN [Group] AS g
                    ON g.[Id] = a.[GroupId]
                LEFT JOIN [Teacher] AS t
                    ON t.[Id] = a.[TeacherId]
                WHERE
                    a.[Id] IS NULL
                    OR a.[TeacherId] IS NULL
                    OR g.[Id] IS NULL
                    OR t.[Id] IS NULL
                    OR a.[StudentUnitType] IS NULL
                    OR a.[TeacherPayUnitType] IS NULL
            )
            BEGIN
                THROW 50000,
                    'Schedule migration aborted: one or more existing Schedule rows cannot be safely backfilled.',
                    1;
            END
            """);

            migrationBuilder.Sql("""
            UPDATE s
            SET
                s.[GroupId] = a.[GroupId],
                s.[TeacherId] = a.[TeacherId],
                s.[StudentChargeAmount] = a.[StudentUnitCost],
                s.[StudentChargeCurrency] = a.[StudentUnitType],
                s.[TeacherPayAmount] = a.[TeacherPayForUnit],
                s.[TeacherPayCurrency] = a.[TeacherPayUnitType]
            FROM [Schedule] AS s
            INNER JOIN [Assignments] AS a
                ON a.[Id] = s.[AssignmentId];
            """);

            
             migrationBuilder.Sql("""
            IF EXISTS
            (
                SELECT 1
                FROM [Schedule]
                WHERE
                    [GroupId] IS NULL
                    OR [TeacherId] IS NULL
                    OR [StudentChargeAmount] IS NULL
                    OR [StudentChargeCurrency] IS NULL
                    OR [TeacherPayAmount] IS NULL
                    OR [TeacherPayCurrency] IS NULL
            )
            BEGIN
                THROW 50001,
                    'Schedule migration aborted: backfill left one or more required values NULL.',
                    1;
            END
            """);

            migrationBuilder.AlterColumn<Guid>(
                name: "GroupId",
                table: "Schedule",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<float>(
                name: "StudentChargeAmount",
                table: "Schedule",
                type: "real",
                nullable: false,
                oldClrType: typeof(float),
                oldType: "real",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StudentChargeCurrency",
                table: "Schedule",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TeacherId",
                table: "Schedule",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<float>(
                name: "TeacherPayAmount",
                table: "Schedule",
                type: "real",
                nullable: false,
                oldClrType: typeof(float),
                oldType: "real",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TeacherPayCurrency",
                table: "Schedule",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);


            migrationBuilder.CreateIndex(
                name: "IX_Schedule_GroupId",
                table: "Schedule",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedule_TeacherId",
                table: "Schedule",
                column: "TeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_Schedule_Assignments_AssignmentId",
                table: "Schedule",
                column: "AssignmentId",
                principalTable: "Assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
                

            migrationBuilder.AddForeignKey(
                name: "FK_Schedule_Group_GroupId",
                table: "Schedule",
                column: "GroupId",
                principalTable: "Group",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedule_Teacher_TeacherId",
                table: "Schedule",
                column: "TeacherId",
                principalTable: "Teacher",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Schedule_Assignments_AssignmentId",
                table: "Schedule");

            migrationBuilder.DropForeignKey(
                name: "FK_Schedule_Group_GroupId",
                table: "Schedule");

            migrationBuilder.DropForeignKey(
                name: "FK_Schedule_Teacher_TeacherId",
                table: "Schedule");

            migrationBuilder.DropIndex(
                name: "IX_Schedule_GroupId",
                table: "Schedule");

            migrationBuilder.DropIndex(
                name: "IX_Schedule_TeacherId",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "StudentChargeAmount",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "StudentChargeCurrency",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "TeacherPayAmount",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "TeacherPayCurrency",
                table: "Schedule");

            migrationBuilder.AddForeignKey(
                name: "FK_Schedule_Assignments_AssignmentId",
                table: "Schedule",
                column: "AssignmentId",
                principalTable: "Assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
