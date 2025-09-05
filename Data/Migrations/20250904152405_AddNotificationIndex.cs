using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarTasca.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TicketId_Type_Channel_Status_SentAt",
                table: "Notifications",
                columns: new[] { "TicketId", "Type", "Channel", "Status", "SentAt" });

            migrationBuilder.DropIndex(
                name: "IX_Notifications_TicketId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Tickets");

            migrationBuilder.AlterColumn<byte>(
                name: "PeopleCount",
                table: "Tickets",
                type: "tinyint unsigned",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
            
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TicketId",
                table: "Notifications",
                column: "TicketId");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_TicketId_Type_Channel_Status_SentAt",
                table: "Notifications");

            migrationBuilder.AlterColumn<int>(
                name: "PeopleCount",
                table: "Tickets",
                type: "int",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "tinyint unsigned");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "Tickets",
                type: "datetime(6)",
                nullable: true);

        }
    }
}
