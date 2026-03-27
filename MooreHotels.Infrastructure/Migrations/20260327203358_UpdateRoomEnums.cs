using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MooreHotels.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRoomEnums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE rooms SET \"Category\" = 'Deluxe' WHERE \"Category\" = 'Business';");
            migrationBuilder.Sql("UPDATE rooms SET \"Category\" = 'PresidentialSuite' WHERE \"Category\" = 'Suite';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE rooms SET \"Category\" = 'Business' WHERE \"Category\" = 'Deluxe';");
            migrationBuilder.Sql("UPDATE rooms SET \"Category\" = 'Suite' WHERE \"Category\" = 'PresidentialSuite';");
        }
    }
}
