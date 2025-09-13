using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarTasca.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsAnonymized : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_PublicId",
                table: "Tickets");

            migrationBuilder.AlterColumn<string>(
                name: "PublicId",
                table: "Tickets",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsAnonymized",
                table: "Customers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAnonymized",
                table: "Customers");

            migrationBuilder.AlterColumn<string>(
                name: "PublicId",
                table: "Tickets",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_PublicId",
                table: "Tickets",
                column: "PublicId",
                unique: true);
        }
    }
}
