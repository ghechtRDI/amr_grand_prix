using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRaceOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GrandPrixRaceOrder",
                table: "Races");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GrandPrixRaceOrder",
                table: "Races",
                type: "integer",
                nullable: true);
        }
    }
}
