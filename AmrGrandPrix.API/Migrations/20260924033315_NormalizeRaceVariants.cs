using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeRaceVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RaceVariants",
                columns: table => new
                {
                    RaceVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RaceSeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Aliases = table.Column<List<string>>(type: "text[]", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsGrandPrixByDefault = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    RecordTimeMale = table.Column<TimeSpan>(type: "interval", nullable: true),
                    RecordTimeFemale = table.Column<TimeSpan>(type: "interval", nullable: true),
                    RecordHolderMale = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RecordHolderFemale = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaceVariants", x => x.RaceVariantId);
                    table.ForeignKey(
                        name: "FK_RaceVariants_RaceSeries_RaceSeriesId",
                        column: x => x.RaceSeriesId,
                        principalTable: "RaceSeries",
                        principalColumn: "RaceSeriesId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "RaceVariantId",
                table: "Races",
                type: "uuid",
                nullable: true);

            // Backfill: every race gets a series (matched/created by race name when it had none),
            // then one variant per distinct (series, CourseVariant) — a blank CourseVariant becomes
            // "Standard". Course records move from the races onto their variant.
            migrationBuilder.Sql("""
                -- Series names become unique: fold same-named series into the oldest one.
                WITH ranked AS (
                    SELECT "RaceSeriesId",
                           FIRST_VALUE("RaceSeriesId") OVER (PARTITION BY "Name" ORDER BY "CreatedAt", "RaceSeriesId") AS "KeepId"
                    FROM "RaceSeries"
                )
                UPDATE "Races" r
                SET "RaceSeriesId" = ranked."KeepId"
                FROM ranked
                WHERE r."RaceSeriesId" = ranked."RaceSeriesId" AND ranked."RaceSeriesId" <> ranked."KeepId";

                DELETE FROM "RaceSeries" s
                WHERE EXISTS (
                    SELECT 1 FROM "RaceSeries" older
                    WHERE older."Name" = s."Name"
                      AND (older."CreatedAt", older."RaceSeriesId") < (s."CreatedAt", s."RaceSeriesId"));

                INSERT INTO "RaceSeries" ("RaceSeriesId", "Name", "CreatedAt")
                SELECT gen_random_uuid(), r."Name", now()
                FROM "Races" r
                WHERE r."RaceSeriesId" IS NULL
                  AND NOT EXISTS (SELECT 1 FROM "RaceSeries" s WHERE s."Name" = r."Name")
                GROUP BY r."Name";

                UPDATE "Races" r
                SET "RaceSeriesId" = s."RaceSeriesId"
                FROM "RaceSeries" s
                WHERE r."RaceSeriesId" IS NULL AND s."Name" = r."Name";

                INSERT INTO "RaceVariants" ("RaceVariantId", "RaceSeriesId", "Name", "Aliases", "IsGrandPrixByDefault",
                                            "DisplayOrder", "RecordTimeMale", "RecordTimeFemale",
                                            "RecordHolderMale", "RecordHolderFemale", "CreatedAt")
                SELECT gen_random_uuid(), v."RaceSeriesId", v."VariantName", '{}', v."AnyGrandPrix",
                       (ROW_NUMBER() OVER (PARTITION BY v."RaceSeriesId" ORDER BY v."VariantName" <> 'Standard', v."VariantName") - 1)::int,
                       v."RecordTimeMale", v."RecordTimeFemale", v."RecordHolderMale", v."RecordHolderFemale", now()
                FROM (
                    SELECT r."RaceSeriesId",
                           COALESCE(NULLIF(TRIM(r."CourseVariant"), ''), 'Standard') AS "VariantName",
                           bool_or(r."IsGrandPrixRace") AS "AnyGrandPrix",
                           MIN(r."RecordTimeMale") AS "RecordTimeMale",
                           MIN(r."RecordTimeFemale") AS "RecordTimeFemale",
                           MAX(r."RecordHolderMale") AS "RecordHolderMale",
                           MAX(r."RecordHolderFemale") AS "RecordHolderFemale"
                    FROM "Races" r
                    GROUP BY 1, 2
                ) v;

                UPDATE "Races" r
                SET "RaceVariantId" = v."RaceVariantId"
                FROM "RaceVariants" v
                WHERE v."RaceSeriesId" = r."RaceSeriesId"
                  AND v."Name" = COALESCE(NULLIF(TRIM(r."CourseVariant"), ''), 'Standard');
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "RaceVariantId",
                table: "Races",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_Races_RaceSeries_RaceSeriesId",
                table: "Races");

            migrationBuilder.DropIndex(
                name: "IX_RaceSeries_Name",
                table: "RaceSeries");

            migrationBuilder.DropIndex(
                name: "IX_Races_RaceSeriesId",
                table: "Races");

            migrationBuilder.DropColumn(name: "CourseVariant", table: "Races");
            migrationBuilder.DropColumn(name: "Name", table: "Races");
            migrationBuilder.DropColumn(name: "RaceSeriesId", table: "Races");
            migrationBuilder.DropColumn(name: "RecordHolderFemale", table: "Races");
            migrationBuilder.DropColumn(name: "RecordHolderMale", table: "Races");
            migrationBuilder.DropColumn(name: "RecordTimeFemale", table: "Races");
            migrationBuilder.DropColumn(name: "RecordTimeMale", table: "Races");

            migrationBuilder.CreateIndex(
                name: "IX_RaceSeries_Name",
                table: "RaceSeries",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Races_RaceVariantId_Year",
                table: "Races",
                columns: new[] { "RaceVariantId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RaceVariants_RaceSeriesId_Name",
                table: "RaceVariants",
                columns: new[] { "RaceSeriesId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Races_RaceVariants_RaceVariantId",
                table: "Races",
                column: "RaceVariantId",
                principalTable: "RaceVariants",
                principalColumn: "RaceVariantId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Races_RaceVariants_RaceVariantId",
                table: "Races");

            migrationBuilder.DropIndex(
                name: "IX_RaceSeries_Name",
                table: "RaceSeries");

            migrationBuilder.DropIndex(
                name: "IX_Races_RaceVariantId_Year",
                table: "Races");

            migrationBuilder.AddColumn<string>(name: "CourseVariant", table: "Races", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Name", table: "Races", type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<Guid>(name: "RaceSeriesId", table: "Races", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<string>(name: "RecordHolderFemale", table: "Races", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "RecordHolderMale", table: "Races", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<TimeSpan>(name: "RecordTimeFemale", table: "Races", type: "interval", nullable: true);
            migrationBuilder.AddColumn<TimeSpan>(name: "RecordTimeMale", table: "Races", type: "interval", nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Races" r
                SET "Name" = s."Name",
                    "RaceSeriesId" = s."RaceSeriesId",
                    "CourseVariant" = NULLIF(v."Name", 'Standard'),
                    "RecordTimeMale" = v."RecordTimeMale",
                    "RecordTimeFemale" = v."RecordTimeFemale",
                    "RecordHolderMale" = v."RecordHolderMale",
                    "RecordHolderFemale" = v."RecordHolderFemale"
                FROM "RaceVariants" v
                JOIN "RaceSeries" s ON s."RaceSeriesId" = v."RaceSeriesId"
                WHERE v."RaceVariantId" = r."RaceVariantId";
                """);

            migrationBuilder.DropColumn(
                name: "RaceVariantId",
                table: "Races");

            migrationBuilder.DropTable(
                name: "RaceVariants");

            migrationBuilder.CreateIndex(
                name: "IX_RaceSeries_Name",
                table: "RaceSeries",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Races_RaceSeriesId",
                table: "Races",
                column: "RaceSeriesId");

            migrationBuilder.AddForeignKey(
                name: "FK_Races_RaceSeries_RaceSeriesId",
                table: "Races",
                column: "RaceSeriesId",
                principalTable: "RaceSeries",
                principalColumn: "RaceSeriesId",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
