using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BE_ECOMMERCE.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserVoucherWallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Vouchers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_UserId",
                table: "Vouchers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_Users_UserId",
                table: "Vouchers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_Users_UserId",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_UserId",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Vouchers");
        }
    }
}
