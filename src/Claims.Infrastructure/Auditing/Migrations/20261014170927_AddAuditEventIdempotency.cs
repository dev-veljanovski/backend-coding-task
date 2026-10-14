using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Claims.Infrastructure.Auditing.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEventIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "CoverAudits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "ClaimAudits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoverAudits_EventId",
                table: "CoverAudits",
                column: "EventId",
                unique: true,
                filter: "[EventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAudits_EventId",
                table: "ClaimAudits",
                column: "EventId",
                unique: true,
                filter: "[EventId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CoverAudits_EventId",
                table: "CoverAudits");

            migrationBuilder.DropIndex(
                name: "IX_ClaimAudits_EventId",
                table: "ClaimAudits");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "CoverAudits");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "ClaimAudits");
        }
    }
}
