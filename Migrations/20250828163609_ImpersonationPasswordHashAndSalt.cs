using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCoreSqlDb.Migrations
{
    /// <inheritdoc />
    public partial class ImpersonationPasswordHashAndSalt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ImpersonationPasswordHash",
                table: "User",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "ImpersonationPasswordSalt",
                table: "User",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImpersonationPasswordHash",
                table: "User");

            migrationBuilder.DropColumn(
                name: "ImpersonationPasswordSalt",
                table: "User");
        }
    }
}
