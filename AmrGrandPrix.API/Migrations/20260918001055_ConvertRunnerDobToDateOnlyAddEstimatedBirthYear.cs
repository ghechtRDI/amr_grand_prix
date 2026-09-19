using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class ConvertRunnerDobToDateOnlyAddEstimatedBirthYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add the new estimated-birth-year column.
            migrationBuilder.AddColumn<int>(
                name: "EstimatedBirthYear",
                table: "Runners",
                type: "integer",
                nullable: true);

            // 2. Every existing Runner.DateOfBirth was written by the results-upload pipeline as a
            //    guess (DateTime.UtcNow.AddYears(-age)) — there was no verified-profile feature
            //    before now. Preserve that guess's year as the new estimate before clearing it.
            migrationBuilder.Sql(
                @"UPDATE ""Runners"" SET ""EstimatedBirthYear"" = EXTRACT(YEAR FROM ""DateOfBirth"")::int WHERE ""DateOfBirth"" IS NOT NULL;");

            // 3. Clear DateOfBirth — from here on it is set only by a runner's own verified profile.
            migrationBuilder.Sql(@"UPDATE ""Runners"" SET ""DateOfBirth"" = NULL;");

            // 4. Narrow the column to date-only, matching how it will be used going forward.
            migrationBuilder.AlterColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Runners",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            // 5. Repair age-category strings persisted under the old "19-29" boundary (which had a
            //    gap at age 18 that fell through to "Unknown"), now that the boundary is "18-29".
            migrationBuilder.Sql(@"UPDATE ""RaceResults"" SET ""AgeCategory"" = '18-29' WHERE ""AgeCategory"" = '19-29';");
            migrationBuilder.Sql(@"UPDATE ""RaceResults"" SET ""AgeCategory"" = '18-29' WHERE ""AgeCategory"" = 'Unknown' AND ""Age"" = 18;");
            migrationBuilder.Sql(@"UPDATE ""GrandPrixPoints"" SET ""AgeCategory"" = '18-29' WHERE ""AgeCategory"" = '19-29';");
            migrationBuilder.Sql(@"UPDATE ""GrandPrixStandings"" SET ""AgeCategory"" = '18-29' WHERE ""AgeCategory"" = '19-29';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Note: this does not restore cleared DateOfBirth values or re-widen recategorized
            // "18-29" rows back to "19-29"/"Unknown" — those data changes are not reversible.
            migrationBuilder.DropColumn(
                name: "EstimatedBirthYear",
                table: "Runners");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateOfBirth",
                table: "Runners",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}
