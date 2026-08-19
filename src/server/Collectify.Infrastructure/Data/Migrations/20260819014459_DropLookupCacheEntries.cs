using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collectify.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropLookupCacheEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LookupCache");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LookupCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FetchedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    JsonResponse = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LookupCache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LookupCache_Provider_Key",
                table: "LookupCache",
                columns: new[] { "Provider", "Key" },
                unique: true);
        }
    }
}
