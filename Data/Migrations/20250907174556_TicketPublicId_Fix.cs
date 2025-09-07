using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarTasca.Data.Migrations
{
    /// <inheritdoc />
    public partial class TicketPublicId_Fix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "Tickets",
                type: "varchar(255)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@"
                UPDATE `Tickets`
                SET `PublicId` = REPLACE(UUID(), '-', '')
                WHERE `PublicId` IS NULL OR `PublicId` = '';
            ");

            migrationBuilder.AlterColumn<string>(
                name: "PublicId",
                table: "Tickets",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_PublicId",
                table: "Tickets",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_PublicId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Tickets");
        }
    }
}
