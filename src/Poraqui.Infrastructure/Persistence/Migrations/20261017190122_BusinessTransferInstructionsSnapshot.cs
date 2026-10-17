using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poraqui.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BusinessTransferInstructionsSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TransferAccountReference",
                table: "Orders",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferInstitution",
                table: "Orders",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferInstructions",
                table: "Orders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferRecipientName",
                table: "Orders",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferAccountReference",
                table: "Businesses",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferInstitution",
                table: "Businesses",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferInstructions",
                table: "Businesses",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferRecipientName",
                table: "Businesses",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TransferAccountReference",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TransferInstitution",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TransferInstructions",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TransferRecipientName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TransferAccountReference",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "TransferInstitution",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "TransferInstructions",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "TransferRecipientName",
                table: "Businesses");
        }
    }
}
