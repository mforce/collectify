using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collectify.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoverImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoverImages",
                columns: table => new
                {
                    Hash = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Bytes = table.Column<byte[]>(type: "BLOB", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverImages", x => x.Hash);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoverImages");
        }
    }
}
