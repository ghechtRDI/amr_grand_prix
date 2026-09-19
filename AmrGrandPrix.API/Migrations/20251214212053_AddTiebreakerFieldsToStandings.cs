using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTiebreakerFieldsToStandings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FourthBestRacePoints",
                table: "GrandPrixStandings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ThirdBestRacePoints",
                table: "GrandPrixStandings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FourthBestRacePoints",
                table: "GrandPrixStandings");

            migrationBuilder.DropColumn(
                name: "ThirdBestRacePoints",
                table: "GrandPrixStandings");
        }
    }
}
