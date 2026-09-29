using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRaceSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RaceSeriesId",
                table: "Races",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RaceSeries",
                columns: table => new
                {
                    RaceSeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaceSeries", x => x.RaceSeriesId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Races_RaceSeriesId",
                table: "Races",
                column: "RaceSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_RaceSeries_Name",
                table: "RaceSeries",
                column: "Name");

            migrationBuilder.AddForeignKey(
                name: "FK_Races_RaceSeries_RaceSeriesId",
                table: "Races",
                column: "RaceSeriesId",
                principalTable: "RaceSeries",
                principalColumn: "RaceSeriesId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Races_RaceSeries_RaceSeriesId",
                table: "Races");

            migrationBuilder.DropTable(
                name: "RaceSeries");

            migrationBuilder.DropIndex(
                name: "IX_Races_RaceSeriesId",
                table: "Races");

            migrationBuilder.DropColumn(
                name: "RaceSeriesId",
                table: "Races");
        }
    }
}
