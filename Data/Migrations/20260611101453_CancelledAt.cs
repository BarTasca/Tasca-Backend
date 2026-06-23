using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarTasca.Data.Migrations
{
    /// <inheritdoc />
    public partial class CancelledAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Tickets",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Tickets");
        }
    }
}
