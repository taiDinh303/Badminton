using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Repositories.Migrations.PostgreSql
{
    public partial class HardenNonPaymentWorkflows : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordRecoveryExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PasswordRecoveryFailures",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PasswordRecoveryHash",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteTokenHash",
                table: "PlayGroups",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryDaySnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CapacityJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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

                LOCK TABLE "PointLedger" IN SHARE ROW EXCLUSIVE MODE;
                DO $conversion$
                BEGIN
                  IF EXISTS (SELECT "UserId" FROM "PointLedger" GROUP BY "UserId" HAVING ABS(SUM("Delta"::bigint)) > 2147483647) THEN
                    RAISE EXCEPTION 'Point balance outside supported conversion range. Review before release.';
                  END IF;
                END $conversion$;
                INSERT INTO "PointLedger" ("UserId", "Delta", "Reason", "LedgerKey", "CreatedAt")
                SELECT u."Id", (TRUNC(COALESCE(b.balance,0) / 1000) - COALESCE(b.balance,0))::integer,
                    'Quy đổi số dư điểm cũ về 1/1000 theo quyết định 25/10/2026', 'unit-v2:' || u."Id", CURRENT_TIMESTAMP
                FROM "Users" u LEFT JOIN (SELECT "UserId", SUM("Delta"::bigint) balance FROM "PointLedger" GROUP BY "UserId") b ON b."UserId"=u."Id"
                WHERE NOT EXISTS (SELECT 1 FROM "PointLedger" p WHERE p."LedgerKey"='unit-v2:' || u."Id");
                ALTER TABLE "InventoryDaySnapshots" ENABLE ROW LEVEL SECURITY;
                REVOKE ALL ON TABLE "InventoryDaySnapshots" FROM PUBLIC;
                REVOKE ALL ON SEQUENCE "InventoryDaySnapshots_Id_seq" FROM PUBLIC;
                DO $privacy$
                DECLARE role_name text;
                BEGIN
                  FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname=role_name) THEN
                      EXECUTE format('REVOKE ALL ON TABLE "InventoryDaySnapshots" FROM %I', role_name);
                      EXECUTE format('REVOKE ALL ON SEQUENCE "InventoryDaySnapshots_Id_seq" FROM %I', role_name);
                    END IF;
                  END LOOP;
                END $privacy$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $rollback$ BEGIN IF EXISTS (SELECT 1 FROM "PointLedger" WHERE "LedgerKey" LIKE 'unit-v2:%') THEN RAISE EXCEPTION 'Point conversion requires an explicit compensating release; automatic downgrade is blocked.'; END IF; END $rollback$;
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
