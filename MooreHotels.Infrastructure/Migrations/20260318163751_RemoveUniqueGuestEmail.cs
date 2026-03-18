using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MooreHotels.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueGuestEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_guests_Email",
                table: "guests");

            migrationBuilder.CreateIndex(
                name: "IX_guests_Email",
                table: "guests",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_guests_Email",
                table: "guests");

            migrationBuilder.CreateIndex(
                name: "IX_guests_Email",
                table: "guests",
                column: "Email",
                unique: true);
        }
    }
}
