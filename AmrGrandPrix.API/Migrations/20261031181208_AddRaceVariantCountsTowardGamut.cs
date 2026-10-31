using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRaceVariantCountsTowardGamut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CountsTowardGamut",
                table: "RaceVariants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Flag the known non-GP courses that count for Gamut purposes (RaceSeedingService only
            // sets this on newly seeded variants)
            migrationBuilder.Sql("""
                UPDATE "RaceVariants" v SET "CountsTowardGamut" = TRUE
                FROM "RaceSeries" s
                WHERE v."RaceSeriesId" = s."RaceSeriesId"
                  AND ((s."Name" = 'Kal''s Knoya Ridge Run' AND v."Name" = 'Dome')
                    OR (s."Name" = 'Government Peak Climb' AND v."Name" = 'Uphill Only'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountsTowardGamut",
                table: "RaceVariants");
        }
    }
}
