using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class AddResultReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResultReports",
                columns: table => new
                {
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunnerNameReported = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DateOfBirthReported = table.Column<DateOnly>(type: "date", nullable: true),
                    RaceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RaceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RaceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReporterEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultReports", x => x.ReportId);
                    table.ForeignKey(
                        name: "FK_ResultReports_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "RaceId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResultReports_RaceId",
                table: "ResultReports",
                column: "RaceId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultReports_Status",
                table: "ResultReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ResultReports_SubmittedAt",
                table: "ResultReports",
                column: "SubmittedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResultReports");
        }
    }
}
