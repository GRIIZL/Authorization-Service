using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointments.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PatientEmail",
                table: "Appointments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAt",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PatientEmail",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "Appointments");
        }
    }
}
