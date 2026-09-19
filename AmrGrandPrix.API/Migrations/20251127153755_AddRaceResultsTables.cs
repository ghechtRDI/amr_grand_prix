using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRaceResultsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Races",
                columns: table => new
                {
                    RaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsGrandPrixRace = table.Column<bool>(type: "boolean", nullable: false),
                    GrandPrixRaceOrder = table.Column<int>(type: "integer", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    CourseVariant = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RecordTimeMale = table.Column<TimeSpan>(type: "interval", nullable: true),
                    RecordTimeFemale = table.Column<TimeSpan>(type: "interval", nullable: true),
                    RecordHolderMale = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RecordHolderFemale = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Races", x => x.RaceId);
                });

            migrationBuilder.CreateTable(
                name: "Runners",
                columns: table => new
                {
                    RunnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Gender = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runners", x => x.RunnerId);
                });

            migrationBuilder.CreateTable(
                name: "UploadBatches",
                columns: table => new
                {
                    UploadBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileType = table.Column<int>(type: "integer", nullable: false),
                    RecordsUploaded = table.Column<int>(type: "integer", nullable: false),
                    UploadedBy = table.Column<string>(type: "text", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadBatches", x => x.UploadBatchId);
                    table.ForeignKey(
                        name: "FK_UploadBatches_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "RaceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GrandPrixStandings",
                columns: table => new
                {
                    StandingId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Division = table.Column<int>(type: "integer", nullable: false),
                    AgeCategory = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TotalPoints = table.Column<int>(type: "integer", nullable: false),
                    RacesCompleted = table.Column<int>(type: "integer", nullable: false),
                    RacesCounted = table.Column<int>(type: "integer", nullable: false),
                    BestRacePoints = table.Column<int>(type: "integer", nullable: false),
                    SecondBestRacePoints = table.Column<int>(type: "integer", nullable: false),
                    RunTheGamutQualified = table.Column<bool>(type: "boolean", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrandPrixStandings", x => x.StandingId);
                    table.ForeignKey(
                        name: "FK_GrandPrixStandings_Runners_RunnerId",
                        column: x => x.RunnerId,
                        principalTable: "Runners",
                        principalColumn: "RunnerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RaceResults",
                columns: table => new
                {
                    ResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    RaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Bib = table.Column<int>(type: "integer", nullable: true),
                    Place = table.Column<int>(type: "integer", nullable: true),
                    PlaceGender = table.Column<int>(type: "integer", nullable: true),
                    PlaceAgeCategory = table.Column<int>(type: "integer", nullable: true),
                    Time = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Age = table.Column<int>(type: "integer", nullable: false),
                    Gender = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsNewRecord = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadedBy = table.Column<string>(type: "text", nullable: true),
                    UploadBatchId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaceResults", x => x.ResultId);
                    table.ForeignKey(
                        name: "FK_RaceResults_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "RaceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RaceResults_Runners_RunnerId",
                        column: x => x.RunnerId,
                        principalTable: "Runners",
                        principalColumn: "RunnerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RaceResults_UploadBatches_UploadBatchId",
                        column: x => x.UploadBatchId,
                        principalTable: "UploadBatches",
                        principalColumn: "UploadBatchId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GrandPrixPoints",
                columns: table => new
                {
                    PointsId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Division = table.Column<int>(type: "integer", nullable: false),
                    AgeCategory = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    IsRecordBonus = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrandPrixPoints", x => x.PointsId);
                    table.ForeignKey(
                        name: "FK_GrandPrixPoints_RaceResults_ResultId",
                        column: x => x.ResultId,
                        principalTable: "RaceResults",
                        principalColumn: "ResultId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GrandPrixPoints_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "RaceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GrandPrixPoints_Runners_RunnerId",
                        column: x => x.RunnerId,
                        principalTable: "Runners",
                        principalColumn: "RunnerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GrandPrixPoints_RaceId",
                table: "GrandPrixPoints",
                column: "RaceId");

            migrationBuilder.CreateIndex(
                name: "IX_GrandPrixPoints_ResultId",
                table: "GrandPrixPoints",
                column: "ResultId");

            migrationBuilder.CreateIndex(
                name: "IX_GrandPrixPoints_RunnerId",
                table: "GrandPrixPoints",
                column: "RunnerId");

            migrationBuilder.CreateIndex(
                name: "IX_GrandPrixPoints_RunnerId_Year_Division",
                table: "GrandPrixPoints",
                columns: new[] { "RunnerId", "Year", "Division" });

            migrationBuilder.CreateIndex(
                name: "IX_GrandPrixPoints_Year_Division",
                table: "GrandPrixPoints",
                columns: new[] { "Year", "Division" });

            migrationBuilder.CreateIndex(
                name: "IX_GrandPrixStandings_RunnerId",
                table: "GrandPrixStandings",
                column: "RunnerId");

            migrationBuilder.CreateIndex(
                name: "IX_GrandPrixStandings_RunnerId_Year",
                table: "GrandPrixStandings",
                columns: new[] { "RunnerId", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_GrandPrixStandings_Year_Division_Rank",
                table: "GrandPrixStandings",
                columns: new[] { "Year", "Division", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_RaceResults_RaceId",
                table: "RaceResults",
                column: "RaceId");

            migrationBuilder.CreateIndex(
                name: "IX_RaceResults_RaceId_RunnerId",
                table: "RaceResults",
                columns: new[] { "RaceId", "RunnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_RaceResults_RunnerId",
                table: "RaceResults",
                column: "RunnerId");

            migrationBuilder.CreateIndex(
                name: "IX_RaceResults_UploadBatchId",
                table: "RaceResults",
                column: "UploadBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Races_Year",
                table: "Races",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_Races_Year_IsGrandPrixRace",
                table: "Races",
                columns: new[] { "Year", "IsGrandPrixRace" });

            migrationBuilder.CreateIndex(
                name: "IX_Runners_Email",
                table: "Runners",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Runners_LastName_FirstName",
                table: "Runners",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_UploadBatches_RaceId",
                table: "UploadBatches",
                column: "RaceId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadBatches_UploadedAt",
                table: "UploadBatches",
                column: "UploadedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GrandPrixPoints");

            migrationBuilder.DropTable(
                name: "GrandPrixStandings");

            migrationBuilder.DropTable(
                name: "RaceResults");

            migrationBuilder.DropTable(
                name: "Runners");

            migrationBuilder.DropTable(
                name: "UploadBatches");

            migrationBuilder.DropTable(
                name: "Races");
        }
    }
}
