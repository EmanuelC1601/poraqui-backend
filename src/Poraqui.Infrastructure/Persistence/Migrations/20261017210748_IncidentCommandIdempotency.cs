using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poraqui.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IncidentCommandIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderIncidents_ReporterUserId",
                table: "OrderIncidents");

            migrationBuilder.AddColumn<Guid>(
                name: "CommandId",
                table: "OrderIncidents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderIncidents_ReporterUserId_CommandId",
                table: "OrderIncidents",
                columns: new[] { "ReporterUserId", "CommandId" },
                unique: true,
                filter: "[CommandId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderIncidents_ReporterUserId_CommandId",
                table: "OrderIncidents");

            migrationBuilder.DropColumn(
                name: "CommandId",
                table: "OrderIncidents");

            migrationBuilder.CreateIndex(
                name: "IX_OrderIncidents_ReporterUserId",
                table: "OrderIncidents",
                column: "ReporterUserId");
        }
    }
}
