using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Repositories.Migrations.PostgreSql
{
    public partial class AddDurableOnlineRefunds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderTransactionDate",
                table: "Payments",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderTransactionId",
                table: "Payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OnlineRefundOperations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    RequestId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    CancelBooking = table.Column<bool>(type: "boolean", nullable: false),
                    ReleaseVoucher = table.Column<bool>(type: "boolean", nullable: false),
                    IsLateReceipt = table.Column<bool>(type: "boolean", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnlineRefundOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnlineRefundOperations_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnlineRefundOperations_PaymentId",
                table: "OnlineRefundOperations",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnlineRefundOperations_RequestId",
                table: "OnlineRefundOperations",
                column: "RequestId",
                unique: true);

            // Backend-only data. Supabase may have default grants on public;
            // never expose refund intents through the browser Data API.
            migrationBuilder.Sql("""
                ALTER TABLE "OnlineRefundOperations" ENABLE ROW LEVEL SECURITY;
                REVOKE ALL ON TABLE "OnlineRefundOperations" FROM PUBLIC;
                REVOKE ALL ON SEQUENCE "OnlineRefundOperations_Id_seq" FROM PUBLIC;
                DO $privacy$
                DECLARE role_name text;
                BEGIN
                  FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
                      EXECUTE format('REVOKE ALL ON TABLE "OnlineRefundOperations" FROM %I', role_name);
                      EXECUTE format('REVOKE ALL ON SEQUENCE "OnlineRefundOperations_Id_seq" FROM %I', role_name);
                    END IF;
                  END LOOP;
                END $privacy$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnlineRefundOperations");

            migrationBuilder.DropColumn(
                name: "ProviderTransactionDate",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderTransactionId",
                table: "Payments");
        }
    }
}
