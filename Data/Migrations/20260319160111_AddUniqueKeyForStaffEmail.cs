using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarTasca.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueKeyForStaffEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StaffUsers_Email",
                table: "StaffUsers",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffUsers_Email",
                table: "StaffUsers");
        }
    }
}
