using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    IdentificationType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IdentificationNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SecondaryPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    LegalName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    TradeName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    ContactPerson = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clients_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_OrganizationId_DisplayName",
                table: "Clients",
                columns: new[] { "OrganizationId", "DisplayName" });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_OrganizationId_IdentificationNumber",
                table: "Clients",
                columns: new[] { "OrganizationId", "IdentificationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_OrganizationId_Status_Type",
                table: "Clients",
                columns: new[] { "OrganizationId", "Status", "Type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clients");
        }
    }
}
