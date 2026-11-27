using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class RunnerNotDuplicates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RunnerNotDuplicates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunnerAId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunnerBId = table.Column<Guid>(type: "uuid", nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DismissedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunnerNotDuplicates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunnerNotDuplicates_Runners_RunnerAId",
                        column: x => x.RunnerAId,
                        principalTable: "Runners",
                        principalColumn: "RunnerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RunnerNotDuplicates_Runners_RunnerBId",
                        column: x => x.RunnerBId,
                        principalTable: "Runners",
                        principalColumn: "RunnerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RunnerNotDuplicates_RunnerAId_RunnerBId",
                table: "RunnerNotDuplicates",
                columns: new[] { "RunnerAId", "RunnerBId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunnerNotDuplicates_RunnerBId",
                table: "RunnerNotDuplicates",
                column: "RunnerBId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunnerNotDuplicates");
        }
    }
}
