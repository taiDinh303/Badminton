using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Repositories.Migrations.PostgreSql
{
    public partial class AddBookingExperience : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Reviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModerationReason",
                table: "Reviews",
                type: "character varying(1500)",
                maxLength: 1500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "Notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "Notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookingBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BookingIdsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookingCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResolvedBy = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingCases_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingCases_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookingHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    Event = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingHistory_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourtFavorites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CourtId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourtFavorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourtFavorites_Courts_CourtId",
                        column: x => x.CourtId,
                        principalTable: "Courts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourtFavorites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DisruptionContacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    IncidentKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ContactedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ContactedBy = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisruptionContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisruptionContacts_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroupRsvps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Attending = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupRsvps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupRsvps_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupRsvps_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HandoverItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ClosedBy = table.Column<int>(type: "integer", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoverItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandoverItems_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    EventKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Title = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboxMessages_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InboxMessages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    RemindersEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MinutesBefore = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<bool>(type: "boolean", nullable: false),
                    Sms = table.Column<bool>(type: "boolean", nullable: false),
                    Zalo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrivacyRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResolvedBy = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivacyRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrivacyRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewReplies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReviewId = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewReplies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewReplies_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReviewId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResolvedBy = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewReports_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffShifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    StaffId = table.Column<int>(type: "integer", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffShifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffShifts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffShifts_Users_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StoredMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerId = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    CourtId = table.Column<int>(type: "integer", nullable: true),
                    ReviewId = table.Column<int>(type: "integer", nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoredMedia_Courts_CourtId",
                        column: x => x.CourtId,
                        principalTable: "Courts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoredMedia_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoredMedia_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingBatches_ActorId_RequestKey",
                table: "BookingBatches",
                columns: new[] { "ActorId", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingCases_BookingId_Status",
                table: "BookingCases",
                columns: new[] { "BookingId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingCases_UserId",
                table: "BookingCases",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingHistory_BookingId_CreatedAt",
                table: "BookingHistory",
                columns: new[] { "BookingId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CourtFavorites_CourtId",
                table: "CourtFavorites",
                column: "CourtId");

            migrationBuilder.CreateIndex(
                name: "IX_CourtFavorites_UserId_CourtId",
                table: "CourtFavorites",
                columns: new[] { "UserId", "CourtId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisruptionContacts_BookingId_IncidentKey",
                table: "DisruptionContacts",
                columns: new[] { "BookingId", "IncidentKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisruptionContacts_BranchId_ContactedAt",
                table: "DisruptionContacts",
                columns: new[] { "BranchId", "ContactedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupRsvps_BookingId_UserId",
                table: "GroupRsvps",
                columns: new[] { "BookingId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupRsvps_UserId",
                table: "GroupRsvps",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverItems_BranchId_ClosedAt",
                table: "HandoverItems",
                columns: new[] { "BranchId", "ClosedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_BookingId",
                table: "InboxMessages",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_UserId_CreatedAt",
                table: "InboxMessages",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_UserId_EventKey",
                table: "InboxMessages",
                columns: new[] { "UserId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId",
                table: "NotificationPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyRequests_UserId_Status",
                table: "PrivacyRequests",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewReplies_ReviewId",
                table: "ReviewReplies",
                column: "ReviewId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewReports_ReviewId_UserId",
                table: "ReviewReports",
                columns: new[] { "ReviewId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffShifts_BranchId_StartsAtUtc",
                table: "StaffShifts",
                columns: new[] { "BranchId", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffShifts_StaffId",
                table: "StaffShifts",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredMedia_CourtId",
                table: "StoredMedia",
                column: "CourtId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredMedia_Key",
                table: "StoredMedia",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoredMedia_OwnerId",
                table: "StoredMedia",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredMedia_ReviewId",
                table: "StoredMedia",
                column: "ReviewId");
            // .NET API owns authorization; Data API roles get no access to these private workflow tables.
            migrationBuilder.Sql("""
                DO $experience_privacy$
                DECLARE table_name text; role_name text; sequence_name text;
                BEGIN
                  FOREACH table_name IN ARRAY ARRAY['BookingBatches', 'BookingCases', 'BookingHistory', 'CourtFavorites', 'DisruptionContacts', 'GroupRsvps', 'HandoverItems', 'InboxMessages', 'NotificationPreferences', 'PrivacyRequests', 'ReviewReplies', 'ReviewReports', 'StaffShifts', 'StoredMedia'] LOOP
                    EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', table_name);
                    EXECUTE format('REVOKE ALL ON TABLE public.%I FROM PUBLIC', table_name);
                    sequence_name := pg_get_serial_sequence(format('public.%I', table_name), 'Id');
                    IF sequence_name IS NOT NULL THEN EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM PUBLIC', sequence_name); END IF;
                    FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
                      IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname=role_name) THEN
                        EXECUTE format('REVOKE ALL ON TABLE public.%I FROM %I', table_name, role_name);
                        IF sequence_name IS NOT NULL THEN EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM %I', sequence_name, role_name); END IF;
                      END IF;
                    END LOOP;
                  END LOOP;
                END $experience_privacy$;
                """);
            migrationBuilder.Sql("""
                INSERT INTO "GroupRsvps" ("BookingId", "UserId", "Attending", "CreatedAt")
                SELECT b."Id",b."UserId",true,CURRENT_TIMESTAMP FROM "Bookings" b
                JOIN "GroupMembers" m ON m."GroupId"=b."GroupId" AND m."UserId"=b."UserId"
                WHERE b."GroupId" IS NOT NULL AND b."DeletedAt" IS NULL AND b."Status" IN ('Pending','Confirmed');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $experience_rollback$
                DECLARE table_name text; populated boolean;
                BEGIN
                  FOREACH table_name IN ARRAY ARRAY['BookingBatches', 'BookingCases', 'BookingHistory', 'CourtFavorites', 'DisruptionContacts', 'GroupRsvps', 'HandoverItems', 'InboxMessages', 'NotificationPreferences', 'PrivacyRequests', 'ReviewReplies', 'ReviewReports', 'StaffShifts', 'StoredMedia'] LOOP
                    EXECUTE format('SELECT EXISTS (SELECT 1 FROM public.%I)', table_name) INTO populated;
                    IF populated THEN RAISE EXCEPTION 'Booking experience contains data; restore backup or use an explicit compensating release.'; END IF;
                  END LOOP;
                END $experience_rollback$;
                """);
            migrationBuilder.DropTable(
                name: "BookingBatches");

            migrationBuilder.DropTable(
                name: "BookingCases");

            migrationBuilder.DropTable(
                name: "BookingHistory");

            migrationBuilder.DropTable(
                name: "CourtFavorites");

            migrationBuilder.DropTable(
                name: "DisruptionContacts");

            migrationBuilder.DropTable(
                name: "GroupRsvps");

            migrationBuilder.DropTable(
                name: "HandoverItems");

            migrationBuilder.DropTable(
                name: "InboxMessages");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropTable(
                name: "PrivacyRequests");

            migrationBuilder.DropTable(
                name: "ReviewReplies");

            migrationBuilder.DropTable(
                name: "ReviewReports");

            migrationBuilder.DropTable(
                name: "StaffShifts");

            migrationBuilder.DropTable(
                name: "StoredMedia");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ModerationReason",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "Notifications");
        }
    }
}
