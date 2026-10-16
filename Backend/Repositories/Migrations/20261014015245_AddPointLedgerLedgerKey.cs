using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddPointLedgerLedgerKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LedgerKey",
                table: "PointLedger",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointLedger_LedgerKey",
                table: "PointLedger",
                column: "LedgerKey",
                unique: true,
                filter: "[LedgerKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PointLedger_LedgerKey",
                table: "PointLedger");

            migrationBuilder.DropColumn(
                name: "LedgerKey",
                table: "PointLedger");
        }
    }
}
