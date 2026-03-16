using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MooreHotels.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGuestField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data migration: Copy values from Guest to Capacity before dropping Guest
            migrationBuilder.Sql("UPDATE rooms SET \"Capacity\" = \"Guest\" WHERE \"Capacity\" = 0 OR \"Capacity\" IS NULL");

            migrationBuilder.DropColumn(
                name: "Guest",
                table: "rooms");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Guest",
                table: "rooms",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
