using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddClubFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Revoked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CoachProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Bio = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachProfiles_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Rental = table.Column<bool>(type: "bit", nullable: false),
                    Stock = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentItems_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroupExpenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    Total = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupExpenses_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MiniTournaments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    OrganizerId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    MaxTeams = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MiniTournaments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MiniTournaments_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MiniTournaments_Users_OrganizerId",
                        column: x => x.OrganizerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlayerSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Area = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Closed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerSessions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerSessions_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CoachSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CoachId = table.Column<int>(type: "int", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    PricePerPerson = table.Column<int>(type: "int", nullable: false),
                    Cancelled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachSlots_CoachProfiles_CoachId",
                        column: x => x.CoachId,
                        principalTable: "CoachProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<int>(type: "int", nullable: false),
                    Rental = table.Column<bool>(type: "bit", nullable: false),
                    RequestKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReturnedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandledBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentOrders_EquipmentItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "EquipmentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentOrders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroupExpenseShares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpenseId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecordedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupExpenseShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupExpenseShares_GroupExpenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "GroupExpenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupExpenseShares_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TournamentTeams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    CaptainId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Players = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Pool = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentTeams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentTeams_MiniTournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "MiniTournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentTeams_Users_CaptainId",
                        column: x => x.CaptainId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlayerSessionMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSessionMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerSessionMembers_PlayerSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "PlayerSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerSessionMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CoachingReservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SlotId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    People = table.Column<int>(type: "int", nullable: false),
                    Total = table.Column<int>(type: "int", nullable: false),
                    RequestKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachingReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachingReservations_CoachSlots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "CoachSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CoachingReservations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TournamentMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    TeamAId = table.Column<int>(type: "int", nullable: false),
                    TeamBId = table.Column<int>(type: "int", nullable: false),
                    Pool = table.Column<int>(type: "int", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ScoreA = table.Column<int>(type: "int", nullable: true),
                    ScoreB = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_MiniTournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "MiniTournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_TournamentTeams_TeamAId",
                        column: x => x.TeamAId,
                        principalTable: "TournamentTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_TournamentTeams_TeamBId",
                        column: x => x.TeamBId,
                        principalTable: "TournamentTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptions_TokenHash",
                table: "CalendarSubscriptions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptions_UserId",
                table: "CalendarSubscriptions",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoachingReservations_SlotId",
                table: "CoachingReservations",
                column: "SlotId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachingReservations_UserId_RequestKey",
                table: "CoachingReservations",
                columns: new[] { "UserId", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoachProfiles_BranchId",
                table: "CoachProfiles",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachSlots_CoachId_StartsAtUtc",
                table: "CoachSlots",
                columns: new[] { "CoachId", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentItems_BranchId_Active",
                table: "EquipmentItems",
                columns: new[] { "BranchId", "Active" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentOrders_ItemId",
                table: "EquipmentOrders",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentOrders_UserId_RequestKey",
                table: "EquipmentOrders",
                columns: new[] { "UserId", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupExpenses_BookingId",
                table: "GroupExpenses",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupExpenseShares_ExpenseId_UserId",
                table: "GroupExpenseShares",
                columns: new[] { "ExpenseId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupExpenseShares_UserId",
                table: "GroupExpenseShares",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MiniTournaments_BranchId",
                table: "MiniTournaments",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_MiniTournaments_OrganizerId",
                table: "MiniTournaments",
                column: "OrganizerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSessionMembers_SessionId_UserId",
                table: "PlayerSessionMembers",
                columns: new[] { "SessionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSessionMembers_UserId",
                table: "PlayerSessionMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSessions_BranchId_StartsAtUtc_Level",
                table: "PlayerSessions",
                columns: new[] { "BranchId", "StartsAtUtc", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSessions_OwnerId",
                table: "PlayerSessions",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_TeamAId",
                table: "TournamentMatches",
                column: "TeamAId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_TeamBId",
                table: "TournamentMatches",
                column: "TeamBId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_TournamentId_TeamAId_TeamBId",
                table: "TournamentMatches",
                columns: new[] { "TournamentId", "TeamAId", "TeamBId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTeams_CaptainId",
                table: "TournamentTeams",
                column: "CaptainId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTeams_TournamentId_CaptainId",
                table: "TournamentTeams",
                columns: new[] { "TournamentId", "CaptainId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTeams_TournamentId_Name",
                table: "TournamentTeams",
                columns: new[] { "TournamentId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [CalendarSubscriptions]) OR EXISTS (SELECT 1 FROM [CoachProfiles])
                OR EXISTS (SELECT 1 FROM [CoachSlots]) OR EXISTS (SELECT 1 FROM [CoachingReservations])
                OR EXISTS (SELECT 1 FROM [EquipmentItems]) OR EXISTS (SELECT 1 FROM [EquipmentOrders])
                OR EXISTS (SELECT 1 FROM [GroupExpenses]) OR EXISTS (SELECT 1 FROM [GroupExpenseShares])
                OR EXISTS (SELECT 1 FROM [MiniTournaments]) OR EXISTS (SELECT 1 FROM [PlayerSessions])
                OR EXISTS (SELECT 1 FROM [PlayerSessionMembers]) OR EXISTS (SELECT 1 FROM [TournamentTeams])
                OR EXISTS (SELECT 1 FROM [TournamentMatches])
                THROW 51000, 'Club workflows contain data; restore backup or use a compensating release.', 1;
                """);
            migrationBuilder.DropTable(
                name: "CalendarSubscriptions");

            migrationBuilder.DropTable(
                name: "CoachingReservations");

            migrationBuilder.DropTable(
                name: "EquipmentOrders");

            migrationBuilder.DropTable(
                name: "GroupExpenseShares");

            migrationBuilder.DropTable(
                name: "PlayerSessionMembers");

            migrationBuilder.DropTable(
                name: "TournamentMatches");

            migrationBuilder.DropTable(
                name: "CoachSlots");

            migrationBuilder.DropTable(
                name: "EquipmentItems");

            migrationBuilder.DropTable(
                name: "GroupExpenses");

            migrationBuilder.DropTable(
                name: "PlayerSessions");

            migrationBuilder.DropTable(
                name: "TournamentTeams");

            migrationBuilder.DropTable(
                name: "CoachProfiles");

            migrationBuilder.DropTable(
                name: "MiniTournaments");
        }
    }
}
