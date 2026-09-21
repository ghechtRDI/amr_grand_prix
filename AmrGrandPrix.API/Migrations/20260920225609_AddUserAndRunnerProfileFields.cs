using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAndRunnerProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "AlternateNames",
                table: "Users",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<string>(
                name: "Hometown",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredName",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "AlternateNames",
                table: "Runners",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<string>(
                name: "PreferredName",
                table: "Runners",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlternateNames",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Hometown",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PreferredName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AlternateNames",
                table: "Runners");

            migrationBuilder.DropColumn(
                name: "PreferredName",
                table: "Runners");
        }
    }
}
