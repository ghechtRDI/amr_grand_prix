using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class AllVariantsUploadBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "RaceId",
                table: "UploadBatches",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateOnly>(
                name: "RaceDate",
                table: "UploadBatches",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RaceSeriesId",
                table: "UploadBatches",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UploadBatches_RaceSeriesId",
                table: "UploadBatches",
                column: "RaceSeriesId");

            migrationBuilder.AddForeignKey(
                name: "FK_UploadBatches_RaceSeries_RaceSeriesId",
                table: "UploadBatches",
                column: "RaceSeriesId",
                principalTable: "RaceSeries",
                principalColumn: "RaceSeriesId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UploadBatches_RaceSeries_RaceSeriesId",
                table: "UploadBatches");

            migrationBuilder.DropIndex(
                name: "IX_UploadBatches_RaceSeriesId",
                table: "UploadBatches");

            migrationBuilder.DropColumn(
                name: "RaceDate",
                table: "UploadBatches");

            migrationBuilder.DropColumn(
                name: "RaceSeriesId",
                table: "UploadBatches");

            migrationBuilder.AlterColumn<Guid>(
                name: "RaceId",
                table: "UploadBatches",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
