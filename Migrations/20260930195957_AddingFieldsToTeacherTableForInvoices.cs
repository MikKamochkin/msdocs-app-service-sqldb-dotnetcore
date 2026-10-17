using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class AddingFieldsToTeacherTableForInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Teacher",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Teacher",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ETransferEmail",
                table: "Teacher",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Teacher",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Teacher",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvinceOrState",
                table: "Teacher",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StreetAddress",
                table: "Teacher",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "Teacher");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Teacher");

            migrationBuilder.DropColumn(
                name: "ETransferEmail",
                table: "Teacher");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Teacher");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Teacher");

            migrationBuilder.DropColumn(
                name: "ProvinceOrState",
                table: "Teacher");

            migrationBuilder.DropColumn(
                name: "StreetAddress",
                table: "Teacher");
        }
    }
}
