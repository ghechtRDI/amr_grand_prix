using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundUploadExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExpectedRows",
                table: "UploadBatches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedText",
                table: "UploadBatches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractionError",
                table: "UploadBatches",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "KnownVariants",
                table: "UploadBatches",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<bool>(
                name: "OnlyTheseVariants",
                table: "UploadBatches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_UploadBatches_Status",
                table: "UploadBatches",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UploadBatches_Status",
                table: "UploadBatches");

            migrationBuilder.DropColumn(
                name: "ExpectedRows",
                table: "UploadBatches");

            migrationBuilder.DropColumn(
                name: "ExtractedText",
                table: "UploadBatches");

            migrationBuilder.DropColumn(
                name: "ExtractionError",
                table: "UploadBatches");

            migrationBuilder.DropColumn(
                name: "KnownVariants",
                table: "UploadBatches");

            migrationBuilder.DropColumn(
                name: "OnlyTheseVariants",
                table: "UploadBatches");
        }
    }
}
