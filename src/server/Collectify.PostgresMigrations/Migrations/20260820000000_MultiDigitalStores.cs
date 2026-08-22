using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collectify.PostgresMigrations.Migrations;

public partial class MultiDigitalStores : Migration
{
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameStoreConnections_OwnerId_Store",
                table: "GameStoreConnections");

            migrationBuilder.DropIndex(
                name: "IX_GameStoreOwnedTitles_OwnerId_Store_ExternalGameId",
                table: "GameStoreOwnedTitles");

            migrationBuilder.RenameColumn(
                name: "DigitalStore",
                table: "Games",
                newName: "DigitalStores");

            migrationBuilder.Sql("""
                UPDATE "Games"
                SET "DigitalStores" =
                    CASE
                        WHEN "IsDigital" AND "DigitalStores" = 0  THEN 1
                        WHEN "IsDigital" AND "DigitalStores" = 1  THEN 2
                        WHEN "IsDigital" AND "DigitalStores" = 2  THEN 4
                        WHEN "IsDigital" AND "DigitalStores" = 3  THEN 8
                        WHEN "IsDigital" AND "DigitalStores" = 4  THEN 16
                        WHEN "IsDigital" AND "DigitalStores" = 5  THEN 32
                        WHEN "IsDigital" AND "DigitalStores" = 99 THEN 64
                        WHEN "IsDigital" AND "DigitalStores" IS NULL THEN 64
                        WHEN "IsDigital" THEN 64
                        ELSE 0
                    END;
                """);

            migrationBuilder.Sql("""
                UPDATE "GameStoreConnections"
                SET "Store" = CASE "Store"
                    WHEN 0  THEN 1
                    WHEN 1  THEN 2
                    WHEN 2  THEN 4
                    WHEN 3  THEN 8
                    WHEN 4  THEN 16
                    WHEN 5  THEN 32
                    WHEN 99 THEN 64
                    ELSE "Store"
                END;

                UPDATE "GameStoreOwnedTitles"
                SET "Store" = CASE "Store"
                    WHEN 0  THEN 1
                    WHEN 1  THEN 2
                    WHEN 2  THEN 4
                    WHEN 3  THEN 8
                    WHEN 4  THEN 16
                    WHEN 5  THEN 32
                    WHEN 99 THEN 64
                    ELSE "Store"
                END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "DigitalStores",
                table: "Games",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "IsDigital",
                table: "Games");

            migrationBuilder.CreateIndex(
                name: "IX_GameStoreConnections_OwnerId_Store",
                table: "GameStoreConnections",
                columns: new[] { "OwnerId", "Store" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameStoreOwnedTitles_OwnerId_Store_ExternalGameId",
                table: "GameStoreOwnedTitles",
                columns: new[] { "OwnerId", "Store", "ExternalGameId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameStoreConnections_OwnerId_Store",
                table: "GameStoreConnections");

            migrationBuilder.DropIndex(
                name: "IX_GameStoreOwnedTitles_OwnerId_Store_ExternalGameId",
                table: "GameStoreOwnedTitles");

            migrationBuilder.AddColumn<bool>(
                name: "IsDigital",
                table: "Games",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE "Games"
                SET "IsDigital" = "DigitalStores" <> 0,
                    "DigitalStores" =
                        CASE
                            WHEN ("DigitalStores" & 64) <> 0 THEN 99
                            WHEN ("DigitalStores" & 32) <> 0 THEN 5
                            WHEN ("DigitalStores" & 16) <> 0 THEN 4
                            WHEN ("DigitalStores" & 8)  <> 0 THEN 3
                            WHEN ("DigitalStores" & 4)  <> 0 THEN 2
                            WHEN ("DigitalStores" & 2)  <> 0 THEN 1
                            WHEN ("DigitalStores" & 1)  <> 0 THEN 0
                            ELSE 0
                        END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "DigitalStores",
                table: "Games",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.RenameColumn(
                name: "DigitalStores",
                table: "Games",
                newName: "DigitalStore");

            migrationBuilder.Sql("""
                UPDATE "GameStoreConnections"
                SET "Store" = CASE "Store"
                    WHEN 1  THEN 0
                    WHEN 2  THEN 1
                    WHEN 4  THEN 2
                    WHEN 8  THEN 3
                    WHEN 16 THEN 4
                    WHEN 32 THEN 5
                    WHEN 64 THEN 99
                    ELSE "Store"
                END;

                UPDATE "GameStoreOwnedTitles"
                SET "Store" = CASE "Store"
                    WHEN 1  THEN 0
                    WHEN 2  THEN 1
                    WHEN 4  THEN 2
                    WHEN 8  THEN 3
                    WHEN 16 THEN 4
                    WHEN 32 THEN 5
                    WHEN 64 THEN 99
                    ELSE "Store"
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_GameStoreConnections_OwnerId_Store",
                table: "GameStoreConnections",
                columns: new[] { "OwnerId", "Store" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameStoreOwnedTitles_OwnerId_Store_ExternalGameId",
                table: "GameStoreOwnedTitles",
                columns: new[] { "OwnerId", "Store", "ExternalGameId" },
                unique: true);
        }
}
