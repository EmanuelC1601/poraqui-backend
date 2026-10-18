using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poraqui.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseProviderSafeOrderAmountConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_NonNegativeAmountsAndValidVersion",
                table: "Orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_NonNegativeAmountsAndValidVersion",
                table: "Orders",
                sql: "[DeliveryFee] >= 0 AND [Total] >= 0 AND ([CourierPaymentAmount] IS NULL OR [CourierPaymentAmount] >= 0) AND [Version] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_NonNegativeAmountsAndValidVersion",
                table: "Orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_NonNegativeAmountsAndValidVersion",
                table: "Orders",
                sql: "[DeliveryFee] >= 0 AND [Total] >= [DeliveryFee] AND ([CourierPaymentAmount] IS NULL OR [CourierPaymentAmount] >= 0) AND [Version] > 0");
        }
    }
}
