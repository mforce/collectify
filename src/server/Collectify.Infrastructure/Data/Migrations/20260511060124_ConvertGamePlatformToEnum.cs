using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collectify.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConvertGamePlatformToEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add the legacy-preservation column.
            migrationBuilder.AddColumn<string>(
                name: "PlatformLegacy",
                table: "Games",
                type: "TEXT",
                nullable: true);

            // 2. Copy the existing free-text Platform values into PlatformLegacy
            //    *before* the column type changes -- the TEXT -> INTEGER swap
            //    below rebuilds the table and silently zeroes anything that
            //    can't be cast, so without this we'd lose them.
            migrationBuilder.Sql(
                "UPDATE Games SET PlatformLegacy = Platform " +
                "WHERE Platform IS NOT NULL AND TRIM(Platform) <> '';");

            // 3. Now flip Platform to INTEGER. EF's SQLite provider rebuilds
            //    the table to honour the type change; surviving TEXT values
            //    fall through to the default (0 = Other) because SQLite's
            //    implicit cast of a non-numeric string yields 0.
            //    GamePlatformBackfill runs at app startup to resolve the
            //    saved-aside PlatformLegacy values into proper enum values.
            migrationBuilder.AlterColumn<int>(
                name: "Platform",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the free-text Platform from whatever PlatformLegacy
            // preserved (resolved rows have PlatformLegacy == NULL, so they
            // come back empty, which matches the old "untyped" behaviour).
            migrationBuilder.AlterColumn<string>(
                name: "Platform",
                table: "Games",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.Sql(
                "UPDATE Games SET Platform = PlatformLegacy " +
                "WHERE PlatformLegacy IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "PlatformLegacy",
                table: "Games");
        }
    }
}
