using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadBatchReviewDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewDraftJson",
                table: "UploadBatches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewDraftSavedAt",
                table: "UploadBatches",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewDraftJson",
                table: "UploadBatches");

            migrationBuilder.DropColumn(
                name: "ReviewDraftSavedAt",
                table: "UploadBatches");
        }
    }
}
