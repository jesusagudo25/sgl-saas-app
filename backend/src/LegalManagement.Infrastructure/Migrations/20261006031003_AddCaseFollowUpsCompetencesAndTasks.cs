using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseFollowUpsCompetencesAndTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaseTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AssignedMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseTasks_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseTasks_Memberships_AssignedMembershipId",
                        column: x => x.AssignedMembershipId,
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseTasks_Memberships_CreatedByMembershipId",
                        column: x => x.CreatedByMembershipId,
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseTasks_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegalCompetences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalCompetences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegalCompetences_LegalCompetences_ParentId",
                        column: x => x.ParentId,
                        principalTable: "LegalCompetences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalCompetences_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseFollowUps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompetenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompetenceDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CompetenceName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    CompetenceDetailName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseFollowUps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseFollowUps_CaseStatuses_CaseStatusId",
                        column: x => x.CaseStatusId,
                        principalTable: "CaseStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseFollowUps_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseFollowUps_LegalCompetences_CompetenceDetailId",
                        column: x => x.CompetenceDetailId,
                        principalTable: "LegalCompetences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseFollowUps_LegalCompetences_CompetenceId",
                        column: x => x.CompetenceId,
                        principalTable: "LegalCompetences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseFollowUps_Memberships_CreatedByMembershipId",
                        column: x => x.CreatedByMembershipId,
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseFollowUps_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseFollowUps_CaseId",
                table: "CaseFollowUps",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseFollowUps_CaseStatusId",
                table: "CaseFollowUps",
                column: "CaseStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseFollowUps_CompetenceDetailId",
                table: "CaseFollowUps",
                column: "CompetenceDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseFollowUps_CompetenceId",
                table: "CaseFollowUps",
                column: "CompetenceId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseFollowUps_CreatedByMembershipId",
                table: "CaseFollowUps",
                column: "CreatedByMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseFollowUps_OrganizationId_CaseId_OccurredAt",
                table: "CaseFollowUps",
                columns: new[] { "OrganizationId", "CaseId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseTasks_AssignedMembershipId",
                table: "CaseTasks",
                column: "AssignedMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTasks_CaseId",
                table: "CaseTasks",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTasks_CreatedByMembershipId",
                table: "CaseTasks",
                column: "CreatedByMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTasks_OrganizationId_CaseId_Status_DueAt",
                table: "CaseTasks",
                columns: new[] { "OrganizationId", "CaseId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseTasks_OrganizationId_Status_DueAt",
                table: "CaseTasks",
                columns: new[] { "OrganizationId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalCompetences_OrganizationId_ParentId_IsActive",
                table: "LegalCompetences",
                columns: new[] { "OrganizationId", "ParentId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalCompetences_OrganizationId_ParentId_Name",
                table: "LegalCompetences",
                columns: new[] { "OrganizationId", "ParentId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegalCompetences_ParentId",
                table: "LegalCompetences",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaseFollowUps");

            migrationBuilder.DropTable(
                name: "CaseTasks");

            migrationBuilder.DropTable(
                name: "LegalCompetences");
        }
    }
}
