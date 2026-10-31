using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRaceVariantIsAgeGroupOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAgeGroupOnly",
                table: "RaceVariants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Flag the known junior-only courses already in the catalog (RaceSeedingService only
            // sets this on newly seeded variants)
            migrationBuilder.Sql("""
                UPDATE "RaceVariants" v SET "IsAgeGroupOnly" = TRUE
                FROM "RaceSeries" s
                WHERE v."RaceSeriesId" = s."RaceSeriesId"
                  AND ((s."Name" = 'Bird Ridge' AND v."Name" = 'Jack''s Run')
                    OR (s."Name" = 'Mount Marathon Race' AND v."Name" = 'Junior')
                    OR (s."Name" = 'Blueberry Rampage' AND v."Name" = 'Junior Blueberry Knoll'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAgeGroupOnly",
                table: "RaceVariants");
        }
    }
}
