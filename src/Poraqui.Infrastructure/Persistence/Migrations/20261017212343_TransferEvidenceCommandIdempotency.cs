using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poraqui.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TransferEvidenceCommandIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CommandId",
                table: "TransferReviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "TransferReviews",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedByUserId",
                table: "TransferReviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferReviews_SubmittedByUserId_CommandId",
                table: "TransferReviews",
                columns: new[] { "SubmittedByUserId", "CommandId" },
                unique: true,
                filter: "[CommandId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_TransferReviews_AspNetUsers_SubmittedByUserId",
                table: "TransferReviews",
                column: "SubmittedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransferReviews_AspNetUsers_SubmittedByUserId",
                table: "TransferReviews");

            migrationBuilder.DropIndex(
                name: "IX_TransferReviews_SubmittedByUserId_CommandId",
                table: "TransferReviews");

            migrationBuilder.DropColumn(
                name: "CommandId",
                table: "TransferReviews");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "TransferReviews");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "TransferReviews");
        }
    }
}
