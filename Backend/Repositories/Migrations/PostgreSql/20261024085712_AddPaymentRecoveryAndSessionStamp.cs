using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations.PostgreSql
{
    public partial class AddPaymentRecoveryAndSessionStamp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayUrl",
                table: "Payments",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReconciliationReason",
                table: "Payments",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReconciliationRequired",
                table: "Payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReturnUrl",
                table: "Payments",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReconciliationRequired_CreatedAt",
                table: "Payments",
                columns: new[] { "ReconciliationRequired", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_ReconciliationRequired_CreatedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GatewayUrl",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReconciliationReason",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReconciliationRequired",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReturnUrl",
                table: "Payments");
        }
    }
}
