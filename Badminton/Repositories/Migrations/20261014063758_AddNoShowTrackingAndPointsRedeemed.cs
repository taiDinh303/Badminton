using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddNoShowTrackingAndPointsRedeemed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NoShowCount",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "NoShowLimited",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "NoShowLimitedPeriod",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "NoShowPeriod",
                table: "Users",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<int>(
                name: "PointsRedeemed",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NoShowCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NoShowLimited",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NoShowLimitedPeriod",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NoShowPeriod",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PointsRedeemed",
                table: "Bookings");
        }
    }
}
