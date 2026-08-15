using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collectify.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Games_Id_OwnerId",
                table: "Games",
                columns: new[] { "Id", "OwnerId" });

            migrationBuilder.CreateTable(
                name: "GameStoreConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    Store = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalAccountId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExternalDisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameStoreConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameStoreOwnedTitles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    Store = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalGameId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ExternalAccountId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    GameId = table.Column<int>(type: "INTEGER", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameStoreOwnedTitles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameStoreOwnedTitles_Games_GameId_OwnerId",
                        columns: x => new { x.GameId, x.OwnerId },
                        principalTable: "Games",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SteamAuthRequests",
                columns: table => new
                {
                    StateHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Consumed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SteamAuthRequests", x => x.StateHash);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameStoreConnections_OwnerId_Store",
                table: "GameStoreConnections",
                columns: new[] { "OwnerId", "Store" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameStoreOwnedTitles_GameId_OwnerId",
                table: "GameStoreOwnedTitles",
                columns: new[] { "GameId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_GameStoreOwnedTitles_OwnerId_Store_ExternalGameId",
                table: "GameStoreOwnedTitles",
                columns: new[] { "OwnerId", "Store", "ExternalGameId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SteamAuthRequests_ExpiresAt",
                table: "SteamAuthRequests",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameStoreConnections");

            migrationBuilder.DropTable(
                name: "GameStoreOwnedTitles");

            migrationBuilder.DropTable(
                name: "SteamAuthRequests");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Games_Id_OwnerId",
                table: "Games");
        }
    }
}
