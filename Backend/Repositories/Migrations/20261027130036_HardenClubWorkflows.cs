using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
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
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SplitMethod",
                table: "GroupExpenses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Equal");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiredAtUtc",
                table: "EquipmentOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverdueNotifiedAtUtc",
                table: "EquipmentOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PickupExpiresAtUtc",
                table: "EquipmentOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CourtBookingId",
                table: "CoachSlots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCourtCheckedAtUtc",
                table: "CoachSlots",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFetchedAtUtc",
                table: "CalendarSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EquipmentConditionReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<int>(type: "int", nullable: false),
                    UnusableQuantity = table.Column<int>(type: "int", nullable: false),
                    Lost = table.Column<bool>(type: "bit", nullable: false),
                    AssessedAmount = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    OldScoreA = table.Column<int>(type: "int", nullable: true),
                    OldScoreB = table.Column<int>(type: "int", nullable: true),
                    ScoreA = table.Column<int>(type: "int", nullable: false),
                    ScoreB = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [EquipmentConditionReports]) OR EXISTS (SELECT 1 FROM [TournamentScoreRevisions])
                OR EXISTS (SELECT 1 FROM [GroupExpenses] WHERE [SplitMethod] = N'Attendance')
                OR EXISTS (SELECT 1 FROM [CoachSlots] WHERE [CourtBookingId] IS NOT NULL)
                OR EXISTS (SELECT 1 FROM [EquipmentOrders] WHERE [PickupExpiresAtUtc] IS NOT NULL OR [ExpiredAtUtc] IS NOT NULL)
                THROW 51000, 'Club hardening contains audit or linked data; use a compensating release.', 1;
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
