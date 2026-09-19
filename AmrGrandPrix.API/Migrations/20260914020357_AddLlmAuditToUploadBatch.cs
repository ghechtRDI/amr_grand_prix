using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmAuditToUploadBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LlmInputTokens",
                table: "UploadBatches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LlmModel",
                table: "UploadBatches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LlmOutputTokens",
                table: "UploadBatches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RawLlmJson",
                table: "UploadBatches",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LlmInputTokens",
                table: "UploadBatches");

            migrationBuilder.DropColumn(
                name: "LlmModel",
                table: "UploadBatches");

            migrationBuilder.DropColumn(
                name: "LlmOutputTokens",
                table: "UploadBatches");

            migrationBuilder.DropColumn(
                name: "RawLlmJson",
                table: "UploadBatches");
        }
    }
}
