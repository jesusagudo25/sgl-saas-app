using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CaseType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResponsibleMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Court = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    Jurisdiction = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    Counterparty = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    OpposingCounsel = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cases_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cases_Memberships_ResponsibleMembershipId",
                        column: x => x.ResponsibleMembershipId,
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cases_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_ClientId",
                table: "Cases",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_OrganizationId_CaseNumber",
                table: "Cases",
                columns: new[] { "OrganizationId", "CaseNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cases_OrganizationId_ClientId",
                table: "Cases",
                columns: new[] { "OrganizationId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_OrganizationId_ResponsibleMembershipId",
                table: "Cases",
                columns: new[] { "OrganizationId", "ResponsibleMembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_OrganizationId_Status_Priority",
                table: "Cases",
                columns: new[] { "OrganizationId", "Status", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_ResponsibleMembershipId",
                table: "Cases",
                column: "ResponsibleMembershipId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cases");
        }
    }
}
