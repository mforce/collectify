using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collectify.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MultiDigitalStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // #91 — Game moves from a single nullable DigitalStore enum value
            // (Steam=0,Gog=1,Epic=2,Xbox=3,Psn=4,Nintendo=5,Other=99) plus a
            // bool IsDigital to a single non-nullable DigitalStores [Flags]
            // bitmask (None=0,Steam=1,Gog=2,Epic=4,Xbox=8,Psn=16,Nintendo=32,
            // Other=64) on which "is digital" is derived.
            //
            // EF cannot infer the semantic mapping, so this migration is
            // hand-written: rename the store column, backfill the old
            // persisted enum ints (and the IsDigital flag) into the new bit
            // values, then drop the now-redundant IsDigital column and pin the
            // bitmask column non-null.

            // Rename the store column (still nullable at this point, holding
            // the OLD persisted enum ints).
            migrationBuilder.RenameColumn(
                name: "DigitalStore",
                table: "Games",
                newName: "DigitalStores");

            // Backfill: map old persisted enum ints to the new flag bits, and
            // fold the IsDigital bool into the bitmask. A row that was marked
            // digital but had no store becomes Other (64), per the issue's
            // backfill rule. A physical row (IsDigital=0) becomes None (0).
            // Anything else (an undefined/retired old int) degrades to physical
            // rather than persisting a bogus bit.
            migrationBuilder.Sql("""
                UPDATE "Games"
                SET "DigitalStores" =
                    CASE
                        WHEN "IsDigital" != 0 AND "DigitalStores" = 0  THEN 1   -- Steam
                        WHEN "IsDigital" != 0 AND "DigitalStores" = 1  THEN 2   -- Gog
                        WHEN "IsDigital" != 0 AND "DigitalStores" = 2  THEN 4   -- Epic
                        WHEN "IsDigital" != 0 AND "DigitalStores" = 3  THEN 8   -- Xbox
                        WHEN "IsDigital" != 0 AND "DigitalStores" = 4  THEN 16  -- Psn
                        WHEN "IsDigital" != 0 AND "DigitalStores" = 5  THEN 32  -- Nintendo
                        WHEN "IsDigital" != 0 AND "DigitalStores" = 99 THEN 64  -- Other
                        WHEN "IsDigital" != 0 AND "DigitalStores" IS NULL THEN 64 -- digital, no store -> Other
                        ELSE 0                                                    -- physical -> None
                    END
                """);

            // Pin the bitmask column non-null with a None (0) default.
            migrationBuilder.AlterColumn<int>(
                name: "DigitalStores",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // IsDigital is now fully derived (DigitalStores != None); drop it.
            migrationBuilder.DropColumn(
                name: "IsDigital",
                table: "Games");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Undo the forward path: restore the IsDigital bool from the
            // bitmask, map new bits back to the old enum ints, then rename the
            // column back to the nullable DigitalStore and make it nullable.
            migrationBuilder.AddColumn<bool>(
                name: "IsDigital",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE "Games"
                SET "IsDigital" = CASE WHEN "DigitalStores" != 0 THEN 1 ELSE 0 END,
                    "DigitalStores" =
                        CASE
                            WHEN ("DigitalStores" & 64)  != 0 THEN 99  -- Other
                            WHEN ("DigitalStores" & 32)  != 0 THEN 5   -- Nintendo
                            WHEN ("DigitalStores" & 16)  != 0 THEN 4   -- Psn
                            WHEN ("DigitalStores" & 8)   != 0 THEN 3   -- Xbox
                            WHEN ("DigitalStores" & 4)   != 0 THEN 2   -- Epic
                            WHEN ("DigitalStores" & 2)   != 0 THEN 1   -- Gog
                            WHEN ("DigitalStores" & 1)   != 0 THEN 0   -- Steam
                            ELSE 0                                      -- None (empty)
                        END
                """);

            migrationBuilder.AlterColumn<int?>(
                name: "DigitalStores",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "DigitalStores",
                table: "Games",
                newName: "DigitalStore");
        }
    }
}
