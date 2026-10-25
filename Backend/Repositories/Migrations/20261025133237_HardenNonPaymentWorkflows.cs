using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    public partial class HardenNonPaymentWorkflows : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordRecoveryExpiresAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PasswordRecoveryFailures",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PasswordRecoveryHash",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteTokenHash",
                table: "PlayGroups",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryDaySnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CapacityJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryDaySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryDaySnapshots_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDaySnapshots_BranchId_Date",
                table: "InventoryDaySnapshots",
                columns: new[] { "BranchId", "Date" },
                unique: true);
            // Append-only conversion: one durable marker per existing account, including zero balances.
            migrationBuilder.Sql("""

                DECLARE @cutover datetime2 = SYSUTCDATETIME();
                DECLARE @rows bigint;
                SELECT @rows = COUNT_BIG(*) FROM PointLedger WITH (TABLOCKX, HOLDLOCK);
                IF EXISTS (SELECT UserId FROM PointLedger GROUP BY UserId HAVING ABS(SUM(CAST(Delta AS bigint))) > 2147483647)
                    THROW 51000, 'Point balance outside supported conversion range. Review before release.', 1;
                INSERT INTO PointLedger (UserId, Delta, Reason, LedgerKey, CreatedAt)
                SELECT u.Id, CAST(COALESCE(b.Balance, 0) / 1000 - COALESCE(b.Balance, 0) AS int),
                    N'Quy đổi số dư điểm cũ về 1/1000 theo quyết định 25/10/2026', CONCAT('unit-v2:', u.Id), @cutover
                FROM Users u LEFT JOIN (SELECT UserId, SUM(CAST(Delta AS bigint)) Balance FROM PointLedger GROUP BY UserId) b ON b.UserId=u.Id
                WHERE NOT EXISTS (SELECT 1 FROM PointLedger p WHERE p.LedgerKey=CONCAT('unit-v2:', u.Id));
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM PointLedger WHERE LedgerKey LIKE 'unit-v2:%') THROW 51000, 'Point conversion requires an explicit compensating release; automatic downgrade is blocked.', 1;
                """);
            migrationBuilder.DropTable(
                name: "InventoryDaySnapshots");

            migrationBuilder.DropColumn(
                name: "PasswordRecoveryExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordRecoveryFailures",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordRecoveryHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "InviteTokenHash",
                table: "PlayGroups");
        }
    }
}
