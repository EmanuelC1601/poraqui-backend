using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poraqui.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CourierAssignmentCommandIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourierAssignmentCommands",
                columns: table => new
                {
                    BusinessUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourierUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourierPaymentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProtectedPickupCode = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourierAssignmentCommands", x => new { x.BusinessUserId, x.CommandId });
                    table.ForeignKey(
                        name: "FK_CourierAssignmentCommands_AspNetUsers_BusinessUserId",
                        column: x => x.BusinessUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourierAssignmentCommands_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourierAssignmentCommands_OrderId",
                table: "CourierAssignmentCommands",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourierAssignmentCommands");
        }
    }
}
