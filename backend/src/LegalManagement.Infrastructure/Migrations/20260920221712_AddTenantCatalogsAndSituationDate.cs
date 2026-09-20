using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantCatalogsAndSituationDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CaseStatusId",
                table: "Cases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CaseTypeId",
                table: "Cases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CourtId",
                table: "Cases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JurisdictionId",
                table: "Cases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SituationDate",
                table: "Cases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CaseStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    IsInnocent = table.Column<bool>(type: "bit", nullable: false),
                    IsGuilty = table.Column<bool>(type: "bit", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseStatuses_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseTypes_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Courts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Courts_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Jurisdictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jurisdictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jurisdictions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Preserve every Sprint 2 value by promoting distinct text values to
            // organization-owned catalog rows before assigning the new FKs.
            migrationBuilder.Sql("""
                INSERT INTO CaseStatuses (Id, OrganizationId, Name, Code, IsActive, IsOpen, IsClosed, IsInnocent, IsGuilty, SortOrder, CreatedAt, UpdatedAt)
                SELECT NEWID(), OrganizationId, Status, Status, 1,
                       CASE WHEN Status = 'CLOSED' THEN 0 ELSE 1 END,
                       CASE WHEN Status = 'CLOSED' THEN 1 ELSE 0 END, 0, 0, 0, SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM Cases GROUP BY OrganizationId, Status;

                INSERT INTO CaseTypes (Id, OrganizationId, Name, IsActive, SortOrder, CreatedAt, UpdatedAt)
                SELECT NEWID(), OrganizationId, CaseType, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM Cases GROUP BY OrganizationId, CaseType;

                INSERT INTO Courts (Id, OrganizationId, Name, IsActive, SortOrder, CreatedAt, UpdatedAt)
                SELECT NEWID(), OrganizationId, Court, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM Cases WHERE Court IS NOT NULL AND LTRIM(RTRIM(Court)) <> '' GROUP BY OrganizationId, Court;

                INSERT INTO Jurisdictions (Id, OrganizationId, Name, IsActive, SortOrder, CreatedAt, UpdatedAt)
                SELECT NEWID(), OrganizationId, Jurisdiction, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM Cases WHERE Jurisdiction IS NOT NULL AND LTRIM(RTRIM(Jurisdiction)) <> '' GROUP BY OrganizationId, Jurisdiction;

                UPDATE c SET CaseStatusId = s.Id FROM Cases c INNER JOIN CaseStatuses s ON s.OrganizationId = c.OrganizationId AND s.Code = c.Status;
                UPDATE c SET CaseTypeId = t.Id FROM Cases c INNER JOIN CaseTypes t ON t.OrganizationId = c.OrganizationId AND t.Name = c.CaseType;
                UPDATE c SET CourtId = t.Id FROM Cases c INNER JOIN Courts t ON t.OrganizationId = c.OrganizationId AND t.Name = c.Court;
                UPDATE c SET JurisdictionId = t.Id FROM Cases c INNER JOIN Jurisdictions t ON t.OrganizationId = c.OrganizationId AND t.Name = c.Jurisdiction;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Cases_CaseStatusId",
                table: "Cases",
                column: "CaseStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_CaseTypeId",
                table: "Cases",
                column: "CaseTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_CourtId",
                table: "Cases",
                column: "CourtId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_JurisdictionId",
                table: "Cases",
                column: "JurisdictionId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_OrganizationId_CaseStatusId",
                table: "Cases",
                columns: new[] { "OrganizationId", "CaseStatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseStatuses_OrganizationId_Code",
                table: "CaseStatuses",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseStatuses_OrganizationId_Name",
                table: "CaseStatuses",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypes_OrganizationId_Name",
                table: "CaseTypes",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Courts_OrganizationId_Name",
                table: "Courts",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jurisdictions_OrganizationId_Name",
                table: "Jurisdictions",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cases_CaseStatuses_CaseStatusId",
                table: "Cases",
                column: "CaseStatusId",
                principalTable: "CaseStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cases_CaseTypes_CaseTypeId",
                table: "Cases",
                column: "CaseTypeId",
                principalTable: "CaseTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cases_Courts_CourtId",
                table: "Cases",
                column: "CourtId",
                principalTable: "Courts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cases_Jurisdictions_JurisdictionId",
                table: "Cases",
                column: "JurisdictionId",
                principalTable: "Jurisdictions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cases_CaseStatuses_CaseStatusId",
                table: "Cases");

            migrationBuilder.DropForeignKey(
                name: "FK_Cases_CaseTypes_CaseTypeId",
                table: "Cases");

            migrationBuilder.DropForeignKey(
                name: "FK_Cases_Courts_CourtId",
                table: "Cases");

            migrationBuilder.DropForeignKey(
                name: "FK_Cases_Jurisdictions_JurisdictionId",
                table: "Cases");

            migrationBuilder.DropTable(
                name: "CaseStatuses");

            migrationBuilder.DropTable(
                name: "CaseTypes");

            migrationBuilder.DropTable(
                name: "Courts");

            migrationBuilder.DropTable(
                name: "Jurisdictions");

            migrationBuilder.DropIndex(
                name: "IX_Cases_CaseStatusId",
                table: "Cases");

            migrationBuilder.DropIndex(
                name: "IX_Cases_CaseTypeId",
                table: "Cases");

            migrationBuilder.DropIndex(
                name: "IX_Cases_CourtId",
                table: "Cases");

            migrationBuilder.DropIndex(
                name: "IX_Cases_JurisdictionId",
                table: "Cases");

            migrationBuilder.DropIndex(
                name: "IX_Cases_OrganizationId_CaseStatusId",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CaseStatusId",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CaseTypeId",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CourtId",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "JurisdictionId",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SituationDate",
                table: "Cases");
        }
    }
}
