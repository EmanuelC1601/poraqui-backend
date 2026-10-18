using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poraqui.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSqlDomainInvariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductOptions_NonNegativePriceDelta",
                table: "ProductOptions",
                sql: "[PriceDelta] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductOptionGroups_SelectionBounds",
                table: "ProductOptionGroups",
                sql: "[MinSelections] >= 0 AND [MaxSelections] >= 1 AND [MinSelections] <= [MaxSelections]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_NonNegativeAmountsAndValidVersion",
                table: "Orders",
                sql: "[DeliveryFee] >= 0 AND [Total] >= [DeliveryFee] AND ([CourierPaymentAmount] IS NULL OR [CourierPaymentAmount] >= 0) AND [Version] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderLines_NonNegativeAmountsAndPositiveQuantity",
                table: "OrderLines",
                sql: "[UnitPrice] >= 0 AND [LineTotal] >= 0 AND [Quantity] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ManualRefundCases_ValidAmounts",
                table: "ManualRefundCases",
                sql: "[RequestedAmount] >= 0 AND ([RefundedAmount] IS NULL OR ([RefundedAmount] >= 0 AND [RefundedAmount] <= [RequestedAmount]))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExternalDeliveryOffers_ValidRemunerationVersionAndAcceptance",
                table: "ExternalDeliveryOffers",
                sql: "[Remuneration] >= 0 AND [Version] > 0 AND ([Status] <> 'Accepted' OR [ProtectedPickupCode] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DeliveryRates_NonNegativeFee",
                table: "DeliveryRates",
                sql: "[Fee] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CourierAvailabilities_ZoneWhenAvailable",
                table: "CourierAvailabilities",
                sql: "[IsAvailable] = 0 OR [ZoneId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CatalogProducts_NonNegativePrice",
                table: "CatalogProducts",
                sql: "[Price] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusinessOpeningHours_ClosedOrValidHours",
                table: "BusinessOpeningHours",
                sql: "([IsClosed] = 1 AND [OpensAt] IS NULL AND [ClosesAt] IS NULL) OR ([IsClosed] = 0 AND [OpensAt] IS NOT NULL AND [ClosesAt] IS NOT NULL AND [OpensAt] < [ClosesAt])");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductOptions_NonNegativePriceDelta",
                table: "ProductOptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductOptionGroups_SelectionBounds",
                table: "ProductOptionGroups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_NonNegativeAmountsAndValidVersion",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderLines_NonNegativeAmountsAndPositiveQuantity",
                table: "OrderLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ManualRefundCases_ValidAmounts",
                table: "ManualRefundCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExternalDeliveryOffers_ValidRemunerationVersionAndAcceptance",
                table: "ExternalDeliveryOffers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DeliveryRates_NonNegativeFee",
                table: "DeliveryRates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CourierAvailabilities_ZoneWhenAvailable",
                table: "CourierAvailabilities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CatalogProducts_NonNegativePrice",
                table: "CatalogProducts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BusinessOpeningHours_ClosedOrValidHours",
                table: "BusinessOpeningHours");
        }
    }
}
