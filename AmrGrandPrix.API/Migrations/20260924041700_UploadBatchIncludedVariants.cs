using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmrGrandPrix.API.Migrations
{
    /// <inheritdoc />
    public partial class UploadBatchIncludedVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<Guid>>(
                name: "IncludedVariantIds",
                table: "UploadBatches",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludedVariantIds",
                table: "UploadBatches");
        }
    }
}
