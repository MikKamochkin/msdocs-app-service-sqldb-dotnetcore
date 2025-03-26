using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    public partial class ChangedIdsToGuid : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ----- USER TABLE -----
            // Drop primary key and the old int ID column, then re-add as Guid.
            migrationBuilder.DropPrimaryKey(
                name: "PK_User",
                table: "User");

            migrationBuilder.DropColumn(
                name: "ID",
                table: "User");

            migrationBuilder.AddColumn<Guid>(
                name: "ID",
                table: "User",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddPrimaryKey(
                name: "PK_User",
                table: "User",
                column: "ID");

            // ----- STUDENT TABLE -----
            // Drop foreign keys that reference Student.ID.
            migrationBuilder.DropForeignKey(
                name: "FK_Contact_Student_StudentID",
                table: "Contact");

            migrationBuilder.DropForeignKey(
                name: "FK_Note_Student_StudentID",
                table: "Note");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Student",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "ID",
                table: "Student");

            migrationBuilder.AddColumn<Guid>(
                name: "ID",
                table: "Student",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Student",
                table: "Student",
                column: "ID");

            // ----- NOTE TABLE -----
            // Note has two columns to change: the PK (ID) and its foreign key (StudentID).
            migrationBuilder.DropForeignKey(
                name: "FK_Note_Student_StudentID",
                table: "Note");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Note",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "ID",
                table: "Note");

            migrationBuilder.AddColumn<Guid>(
                name: "ID",
                table: "Note",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.DropColumn(
                name: "StudentID",
                table: "Note");

            migrationBuilder.AddColumn<Guid>(
                name: "StudentID",
                table: "Note",
                type: "uniqueidentifier",
                nullable: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Note",
                table: "Note",
                column: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Note_Student_StudentID",
                table: "Note",
                column: "StudentID",
                principalTable: "Student",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            // ----- CONTACT TABLE -----
            // Contact: change the PK (ID) and the foreign key (StudentID).
            migrationBuilder.DropForeignKey(
                name: "FK_Contact_Student_StudentID",
                table: "Contact");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Contact",
                table: "Contact");

            migrationBuilder.DropColumn(
                name: "ID",
                table: "Contact");

            migrationBuilder.AddColumn<Guid>(
                name: "ID",
                table: "Contact",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.DropColumn(
                name: "StudentID",
                table: "Contact");

            migrationBuilder.AddColumn<Guid>(
                name: "StudentID",
                table: "Contact",
                type: "uniqueidentifier",
                nullable: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Contact",
                table: "Contact",
                column: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Contact_Student_StudentID",
                table: "Contact",
                column: "StudentID",
                principalTable: "Student",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ----- CONTACT TABLE (Down) -----
            migrationBuilder.DropForeignKey(
                name: "FK_Contact_Student_StudentID",
                table: "Contact");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Contact",
                table: "Contact");

            migrationBuilder.DropColumn(
                name: "StudentID",
                table: "Contact");

            migrationBuilder.DropColumn(
                name: "ID",
                table: "Contact");

            migrationBuilder.AddColumn<int>(
                name: "ID",
                table: "Contact",
                type: "int",
                nullable: false)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<int>(
                name: "StudentID",
                table: "Contact",
                type: "int",
                nullable: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Contact",
                table: "Contact",
                column: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Contact_Student_StudentID",
                table: "Contact",
                column: "StudentID",
                principalTable: "Student",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            // ----- NOTE TABLE (Down) -----
            migrationBuilder.DropForeignKey(
                name: "FK_Note_Student_StudentID",
                table: "Note");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Note",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "StudentID",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "ID",
                table: "Note");

            migrationBuilder.AddColumn<int>(
                name: "ID",
                table: "Note",
                type: "int",
                nullable: false)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<int>(
                name: "StudentID",
                table: "Note",
                type: "int",
                nullable: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Note",
                table: "Note",
                column: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Note_Student_StudentID",
                table: "Note",
                column: "StudentID",
                principalTable: "Student",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            // ----- STUDENT TABLE (Down) -----
            migrationBuilder.DropForeignKey(
                name: "FK_Note_Student_StudentID",
                table: "Note");

            migrationBuilder.DropForeignKey(
                name: "FK_Contact_Student_StudentID",
                table: "Contact");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Student",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "ID",
                table: "Student");

            migrationBuilder.AddColumn<int>(
                name: "ID",
                table: "Student",
                type: "int",
                nullable: false)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Student",
                table: "Student",
                column: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Note_Student_StudentID",
                table: "Note",
                column: "StudentID",
                principalTable: "Student",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            // ----- USER TABLE (Down) -----
            migrationBuilder.DropPrimaryKey(
                name: "PK_User",
                table: "User");

            migrationBuilder.DropColumn(
                name: "ID",
                table: "User");

            migrationBuilder.AddColumn<int>(
                name: "ID",
                table: "User",
                type: "int",
                nullable: false)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_User",
                table: "User",
                column: "ID");
        }
    }
}
