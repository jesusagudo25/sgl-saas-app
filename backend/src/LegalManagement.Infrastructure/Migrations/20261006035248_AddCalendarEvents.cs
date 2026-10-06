using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AssignedMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AllDay = table.Column<bool>(type: "bit", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MeetingUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvents", x => x.Id);
                    table.CheckConstraint("CK_CalendarEvents_Dates", "[EndsAt] >= [StartsAt]");
                    table.ForeignKey(
                        name: "FK_CalendarEvents_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_Memberships_AssignedMembershipId",
                        column: x => x.AssignedMembershipId,
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_Memberships_CreatedByMembershipId",
                        column: x => x.CreatedByMembershipId,
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_AssignedMembershipId",
                table: "CalendarEvents",
                column: "AssignedMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_CaseId",
                table: "CalendarEvents",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_CreatedByMembershipId",
                table: "CalendarEvents",
                column: "CreatedByMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_OrganizationId_AssignedMembershipId_StartsAt",
                table: "CalendarEvents",
                columns: new[] { "OrganizationId", "AssignedMembershipId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_OrganizationId_CaseId_StartsAt",
                table: "CalendarEvents",
                columns: new[] { "OrganizationId", "CaseId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_OrganizationId_StartsAt_EndsAt",
                table: "CalendarEvents",
                columns: new[] { "OrganizationId", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_OrganizationId_Status_StartsAt",
                table: "CalendarEvents",
                columns: new[] { "OrganizationId", "Status", "StartsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarEvents");
        }
    }
}
