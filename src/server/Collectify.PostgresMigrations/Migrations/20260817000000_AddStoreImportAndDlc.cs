using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Collectify.PostgresMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreImportAndDlc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentGameId",
                table: "Games",
                type: "integer",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Games_Id_OwnerId",
                table: "Games",
                columns: new[] { "Id", "OwnerId" });

            migrationBuilder.CreateTable(
                name: "GameStoreConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Store = table.Column<int>(type: "integer", nullable: false),
                    ExternalAccountId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameStoreConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameStoreOwnedTitles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Store = table.Column<int>(type: "integer", nullable: false),
                    ExternalGameId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalAccountId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ParentExternalGameId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    GameId = table.Column<int>(type: "integer", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    StateHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Consumed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SteamAuthRequests", x => x.StateHash);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Games_ParentGameId",
                table: "Games",
                column: "ParentGameId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_ParentGameId_OwnerId",
                table: "Games",
                columns: new[] { "ParentGameId", "OwnerId" });

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

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Games_ParentGameId_OwnerId",
                table: "Games",
                columns: new[] { "ParentGameId", "OwnerId" },
                principalTable: "Games",
                principalColumns: new[] { "Id", "OwnerId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_Games_ParentGameId_OwnerId",
                table: "Games");

            migrationBuilder.DropTable(
                name: "GameStoreConnections");

            migrationBuilder.DropTable(
                name: "GameStoreOwnedTitles");

            migrationBuilder.DropTable(
                name: "SteamAuthRequests");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Games_Id_OwnerId",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Games_ParentGameId",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Games_ParentGameId_OwnerId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ParentGameId",
                table: "Games");
        }
    }
}
