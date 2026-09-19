using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRunnerClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RunnerId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RunnerClaims",
                columns: table => new
                {
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "text", nullable: false),
                    RunnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunnerClaims", x => x.ClaimId);
                    table.ForeignKey(
                        name: "FK_RunnerClaims_Runners_RunnerId",
                        column: x => x.RunnerId,
                        principalTable: "Runners",
                        principalColumn: "RunnerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RunnerClaims_Users_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_RunnerId",
                table: "Users",
                column: "RunnerId");

            migrationBuilder.CreateIndex(
                name: "IX_RunnerClaims_ApplicationUserId",
                table: "RunnerClaims",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RunnerClaims_RunnerId",
                table: "RunnerClaims",
                column: "RunnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Runners_RunnerId",
                table: "Users",
                column: "RunnerId",
                principalTable: "Runners",
                principalColumn: "RunnerId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Runners_RunnerId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "RunnerClaims");

            migrationBuilder.DropIndex(
                name: "IX_Users_RunnerId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RunnerId",
                table: "Users");
        }
    }
}
