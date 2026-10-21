using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poraqui.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueActiveExternalOfferPerCourier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ExternalDeliveryOffers_OrderId_CourierUserId",
                table: "ExternalDeliveryOffers",
                columns: new[] { "OrderId", "CourierUserId" },
                unique: true,
                filter: "[Status] = 'Offered'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExternalDeliveryOffers_OrderId_CourierUserId",
                table: "ExternalDeliveryOffers");
        }
    }
}
