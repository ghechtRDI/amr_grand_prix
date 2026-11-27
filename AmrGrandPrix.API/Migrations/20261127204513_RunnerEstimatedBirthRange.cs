using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <summary>
    /// Replaces Runners.EstimatedBirthYear with an estimated birth date range
    /// (EstimatedDobEarliest/EstimatedDobLatest); EstimatedBirthYear becomes a computed C# property.
    /// The backfill mirrors BirthDateRange.FromResult/Combine — see DOCS/RUNNER_MATCHING.md.
    /// </summary>
    public partial class RunnerEstimatedBirthRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EstimatedDobEarliest",
                table: "Runners",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EstimatedDobLatest",
                table: "Runners",
                type: "date",
                nullable: true);

            // For each runner: seed the range from the old estimate (EstimatedBirthYear = race year
            // - age, so the true birth year is that year or the one before - this keeps any admin
            // "Update estimated age" overrides as the anchor), then fold in every result's age (or,
            // failing that, its age category) in race-date order. Each result's window is
            // intersected in; a result that contradicts the range so far is skipped. If nothing
            // narrowed a seeded range to a single age, fall back to the old year exactly so no
            // runner loses their estimated age. NULL bounds are open-ended; GREATEST/LEAST ignore
            // NULLs, which is exactly the intersection of open-ended bounds.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    rn RECORD;
                    res RECORD;
                    e date;
                    l date;
                    known boolean;
                    min_age int;
                    max_age int;
                    we date;
                    wl date;
                    ne date;
                    nl date;
                BEGIN
                    FOR rn IN SELECT "RunnerId", "EstimatedBirthYear" FROM "Runners" LOOP
                        IF rn."EstimatedBirthYear" IS NOT NULL THEN
                            e := make_date(rn."EstimatedBirthYear" - 1, 1, 1);
                            l := make_date(rn."EstimatedBirthYear", 12, 31);
                            known := true;
                        ELSE
                            e := NULL;
                            l := NULL;
                            known := false;
                        END IF;

                        FOR res IN
                            SELECT rr."Age", rr."AgeCategory", ra."Date"
                            FROM "RaceResults" rr
                            JOIN "Races" ra ON ra."RaceId" = rr."RaceId"
                            WHERE rr."RunnerId" = rn."RunnerId"
                              AND (rr."Age" IS NOT NULL OR rr."AgeCategory" IS NOT NULL)
                            ORDER BY ra."Date", rr."CreatedAt"
                        LOOP
                            IF res."Age" IS NOT NULL THEN
                                min_age := res."Age";
                                max_age := res."Age";
                            ELSE
                                CASE lower(trim(res."AgeCategory"))
                                    WHEN '17 and under' THEN min_age := 0;  max_age := 17;
                                    WHEN '18-29'        THEN min_age := 18; max_age := 29;
                                    WHEN '30-39'        THEN min_age := 30; max_age := 39;
                                    WHEN '40-49'        THEN min_age := 40; max_age := 49;
                                    WHEN '50-59'        THEN min_age := 50; max_age := 59;
                                    WHEN '60-69'        THEN min_age := 60; max_age := 69;
                                    WHEN '70-79'        THEN min_age := 70; max_age := 79;
                                    WHEN '80-89'        THEN min_age := 80; max_age := 89;
                                    WHEN '90+'          THEN min_age := 90; max_age := NULL;
                                    ELSE CONTINUE;
                                END CASE;
                            END IF;

                            we := CASE WHEN max_age IS NULL THEN NULL
                                       ELSE (res."Date" - make_interval(years => max_age + 1))::date + 1 END;
                            wl := (res."Date" - make_interval(years => min_age))::date;

                            IF NOT known THEN
                                e := we;
                                l := wl;
                                known := true;
                            ELSE
                                ne := GREATEST(e, we);
                                nl := LEAST(l, wl);
                                IF ne IS NULL OR nl IS NULL OR ne <= nl THEN
                                    e := ne;
                                    l := nl;
                                END IF;
                            END IF;
                        END LOOP;

                        IF rn."EstimatedBirthYear" IS NOT NULL AND (l - e) > 366 THEN
                            e := make_date(rn."EstimatedBirthYear", 1, 1);
                            l := make_date(rn."EstimatedBirthYear", 12, 31);
                        END IF;

                        IF known THEN
                            UPDATE "Runners"
                            SET "EstimatedDobEarliest" = e, "EstimatedDobLatest" = l
                            WHERE "RunnerId" = rn."RunnerId";
                        END IF;
                    END LOOP;
                END $$;
                """);

            migrationBuilder.DropColumn(
                name: "EstimatedBirthYear",
                table: "Runners");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstimatedBirthYear",
                table: "Runners",
                type: "integer",
                nullable: true);

            // Same rule as BirthDateRange.EstimatedYear: the middle of a range no wider than one age.
            migrationBuilder.Sql("""
                UPDATE "Runners"
                SET "EstimatedBirthYear" = extract(year FROM "EstimatedDobEarliest" + ("EstimatedDobLatest" - "EstimatedDobEarliest") / 2)::int
                WHERE "EstimatedDobEarliest" IS NOT NULL
                  AND "EstimatedDobLatest" IS NOT NULL
                  AND "EstimatedDobLatest" - "EstimatedDobEarliest" <= 366;
                """);

            migrationBuilder.DropColumn(
                name: "EstimatedDobEarliest",
                table: "Runners");

            migrationBuilder.DropColumn(
                name: "EstimatedDobLatest",
                table: "Runners");
        }
    }
}
