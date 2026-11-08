using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class UniqueRaceResultPerRunner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Duplicate results from before this constraint would make CreateIndex fail with a bare
            // unique violation. Fail first with the offending race/runner pairs instead, so they
            // can be resolved by hand (delete the extra result in Results Management, or delete
            // the duplicate upload batch) - which result is the right one isn't decidable here.
            migrationBuilder.Sql("""
                DO $$
                DECLARE dupes text;
                BEGIN
                    SELECT string_agg(format('race %s / runner %s (%s results)', "RaceId", "RunnerId", n), '; ')
                    INTO dupes
                    FROM (SELECT "RaceId", "RunnerId", count(*) AS n
                          FROM "RaceResults"
                          GROUP BY "RaceId", "RunnerId"
                          HAVING count(*) > 1) d;
                    IF dupes IS NOT NULL THEN
                        RAISE EXCEPTION 'Cannot add unique (RaceId, RunnerId) index on RaceResults; remove these duplicate results first: %', dupes;
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_RaceResults_RaceId_RunnerId",
                table: "RaceResults");

            migrationBuilder.CreateIndex(
                name: "IX_RaceResults_RaceId_RunnerId",
                table: "RaceResults",
                columns: new[] { "RaceId", "RunnerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RaceResults_RaceId_RunnerId",
                table: "RaceResults");

            migrationBuilder.CreateIndex(
                name: "IX_RaceResults_RaceId_RunnerId",
                table: "RaceResults",
                columns: new[] { "RaceId", "RunnerId" });
        }
    }
}
