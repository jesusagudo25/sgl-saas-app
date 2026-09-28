using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandClientLegalProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Clients",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Clients",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPersonEmail",
                table: "Clients",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPersonIdentification",
                table: "Clients",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPersonPhone",
                table: "Clients",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Clients",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Dv",
                table: "Clients",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MobilePhone",
                table: "Clients",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvinceOrState",
                table: "Clients",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Clients",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ContactPersonEmail",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ContactPersonIdentification",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ContactPersonPhone",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Dv",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MobilePhone",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ProvinceOrState",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Clients");

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Clients",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
