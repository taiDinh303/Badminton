using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations.PostgreSql
{
    public partial class AddBookingRequestFingerprint : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "Bookings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                table: "Bookings");
        }
    }
}
