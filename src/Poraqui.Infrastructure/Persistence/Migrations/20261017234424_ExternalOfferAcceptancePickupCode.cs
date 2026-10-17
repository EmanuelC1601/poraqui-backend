using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poraqui.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExternalOfferAcceptancePickupCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProtectedPickupCode",
                table: "ExternalDeliveryOffers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProtectedPickupCode",
                table: "ExternalDeliveryOffers");
        }
    }
}
