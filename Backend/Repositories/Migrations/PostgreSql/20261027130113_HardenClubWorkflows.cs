using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Repositories.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class HardenClubWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttendedMinutes",
                table: "GroupExpenseShares",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SplitMethod",
                table: "GroupExpenses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Equal");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiredAtUtc",
                table: "EquipmentOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverdueNotifiedAtUtc",
                table: "EquipmentOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PickupExpiresAtUtc",
                table: "EquipmentOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CourtBookingId",
                table: "CoachSlots",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCourtCheckedAtUtc",
                table: "CoachSlots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFetchedAtUtc",
                table: "CalendarSubscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EquipmentConditionReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    UnusableQuantity = table.Column<int>(type: "integer", nullable: false),
                    Lost = table.Column<bool>(type: "boolean", nullable: false),
                    AssessedAmount = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentConditionReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentConditionReports_EquipmentOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "EquipmentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentConditionReports_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TournamentScoreRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatchId = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    OldScoreA = table.Column<int>(type: "integer", nullable: true),
                    OldScoreB = table.Column<int>(type: "integer", nullable: true),
                    ScoreA = table.Column<int>(type: "integer", nullable: false),
                    ScoreB = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentScoreRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentScoreRevisions_TournamentMatches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "TournamentMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentScoreRevisions_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoachSlots_CourtBookingId",
                table: "CoachSlots",
                column: "CourtBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentConditionReports_ActorId",
                table: "EquipmentConditionReports",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentConditionReports_OrderId",
                table: "EquipmentConditionReports",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentScoreRevisions_ActorId",
                table: "TournamentScoreRevisions",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentScoreRevisions_MatchId_Revision",
                table: "TournamentScoreRevisions",
                columns: new[] { "MatchId", "Revision" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CoachSlots_Bookings_CourtBookingId",
                table: "CoachSlots",
                column: "CourtBookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                DO $club_hardening_privacy$
                DECLARE table_name text; role_name text; sequence_name text;
                BEGIN
                  FOREACH table_name IN ARRAY ARRAY['EquipmentConditionReports','TournamentScoreRevisions'] LOOP
                    EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', table_name);
                    EXECUTE format('REVOKE ALL ON TABLE public.%I FROM PUBLIC', table_name);
                    sequence_name := pg_get_serial_sequence(format('public.%I', table_name), 'Id');
                    IF sequence_name IS NOT NULL THEN EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM PUBLIC', sequence_name); END IF;
                    FOREACH role_name IN ARRAY ARRAY['anon','authenticated'] LOOP
                      IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname=role_name) THEN
                        EXECUTE format('REVOKE ALL ON TABLE public.%I FROM %I', table_name, role_name);
                        IF sequence_name IS NOT NULL THEN EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM %I', sequence_name, role_name); END IF;
                      END IF;
                    END LOOP;
                  END LOOP;
                END $club_hardening_privacy$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $club_hardening_rollback$ BEGIN
                  IF EXISTS (SELECT 1 FROM "EquipmentConditionReports") OR EXISTS (SELECT 1 FROM "TournamentScoreRevisions")
                    OR EXISTS (SELECT 1 FROM "GroupExpenses" WHERE "SplitMethod" = 'Attendance')
                    OR EXISTS (SELECT 1 FROM "CoachSlots" WHERE "CourtBookingId" IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM "EquipmentOrders" WHERE "PickupExpiresAtUtc" IS NOT NULL OR "ExpiredAtUtc" IS NOT NULL)
                  THEN RAISE EXCEPTION 'Club hardening contains audit or linked data; use a compensating release.'; END IF;
                END $club_hardening_rollback$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_CoachSlots_Bookings_CourtBookingId",
                table: "CoachSlots");

            migrationBuilder.DropTable(
                name: "EquipmentConditionReports");

            migrationBuilder.DropTable(
                name: "TournamentScoreRevisions");

            migrationBuilder.DropIndex(
                name: "IX_CoachSlots_CourtBookingId",
                table: "CoachSlots");

            migrationBuilder.DropColumn(
                name: "AttendedMinutes",
                table: "GroupExpenseShares");

            migrationBuilder.DropColumn(
                name: "SplitMethod",
                table: "GroupExpenses");

            migrationBuilder.DropColumn(
                name: "ExpiredAtUtc",
                table: "EquipmentOrders");

            migrationBuilder.DropColumn(
                name: "OverdueNotifiedAtUtc",
                table: "EquipmentOrders");

            migrationBuilder.DropColumn(
                name: "PickupExpiresAtUtc",
                table: "EquipmentOrders");

            migrationBuilder.DropColumn(
                name: "CourtBookingId",
                table: "CoachSlots");

            migrationBuilder.DropColumn(
                name: "LastCourtCheckedAtUtc",
                table: "CoachSlots");

            migrationBuilder.DropColumn(
                name: "LastFetchedAtUtc",
                table: "CalendarSubscriptions");
        }
    }
}
