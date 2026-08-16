using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collectify.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixDlcFkOwnerScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_Games_ParentGameId",
                table: "Games");

            migrationBuilder.CreateIndex(
                name: "IX_Games_ParentGameId_OwnerId",
                table: "Games",
                columns: new[] { "ParentGameId", "OwnerId" });

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

            migrationBuilder.DropIndex(
                name: "IX_Games_ParentGameId_OwnerId",
                table: "Games");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Games_ParentGameId",
                table: "Games",
                column: "ParentGameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
