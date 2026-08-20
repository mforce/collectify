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
                        WHEN "IsDigital" != 0 THEN 64          -- digital with an unrecognised old store int -> preserve digital as Other
                        ELSE 0                                 -- physical -> None
                    END
                """);

            // Steam-ledger discriminators (GameStoreConnections.Store and
            // GameStoreOwnedTitles.Store) also persist the OLD DigitalStore
            // enum int (a connected Steam account rows has Store=0, and the
            // import queries Store == DigitalStore.Steam which is 1 after the
            // renumber). Without this remap an existing Steam connection would
            // read as "disconnected" and previously-imported titles would stop
            // deduping (re-import → duplicate games). Both columns are
            // single-value discriminators: map the old int to the new bit.
            migrationBuilder.Sql("""
                UPDATE "GameStoreConnections"
                SET "Store" = CASE "Store"
                    WHEN 0  THEN 1    -- Steam
                    WHEN 1  THEN 2    -- Gog
                    WHEN 2  THEN 4    -- Epic
                    WHEN 3  THEN 8    -- Xbox
                    WHEN 4  THEN 16   -- Psn
                    WHEN 5  THEN 32   -- Nintendo
                    WHEN 99 THEN 64   -- Other
                    ELSE "Store"      -- already a new-style value / unknown: leave
                END;
                UPDATE "GameStoreOwnedTitles"
                SET "Store" = CASE "Store"
                    WHEN 0  THEN 1    -- Steam
                    WHEN 1  THEN 2    -- Gog
                    WHEN 2  THEN 4    -- Epic
                    WHEN 3  THEN 8    -- Xbox
                    WHEN 4  THEN 16   -- Psn
                    WHEN 5  THEN 32   -- Nintendo
                    WHEN 99 THEN 64   -- Other
                    ELSE "Store"      -- already a new-style value / unknown: leave
                END;
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
            // nullable DigitalStores column back to DigitalStore.
            //
            // Order matters on SQLite: AlterColumn triggers a table rebuild
            // generated from the *target* model snapshot (whose column is named
            // DigitalStore), so the physical column must already be renamed
            // before the constraint change. Rename first, then alter.
            migrationBuilder.RenameColumn(
                name: "DigitalStores",
                table: "Games",
                newName: "DigitalStore");

            migrationBuilder.AddColumn<bool>(
                name: "IsDigital",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE "Games"
                SET "IsDigital" = CASE WHEN "DigitalStore" != 0 THEN 1 ELSE 0 END,
                    "DigitalStore" =
                        CASE
                            WHEN ("DigitalStore" & 64)  != 0 THEN 99  -- Other
                            WHEN ("DigitalStore" & 32)  != 0 THEN 5   -- Nintendo
                            WHEN ("DigitalStore" & 16)  != 0 THEN 4   -- Psn
                            WHEN ("DigitalStore" & 8)   != 0 THEN 3   -- Xbox
                            WHEN ("DigitalStore" & 4)   != 0 THEN 2   -- Epic
                            WHEN ("DigitalStore" & 2)   != 0 THEN 1   -- Gog
                            WHEN ("DigitalStore" & 1)   != 0 THEN 0   -- Steam
                            ELSE 0                                     -- None (empty)
                        END;
                """);

            migrationBuilder.AlterColumn<int?>(
                name: "DigitalStore",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            // Restore the old Steam-ledger discriminator ints (reverse of the
            // forward remap), so a downgraded DB's connection parity holds.
            migrationBuilder.Sql("""
                UPDATE "GameStoreConnections"
                SET "Store" = CASE "Store"
                    WHEN 1  THEN 0    -- Steam
                    WHEN 2  THEN 1    -- Gog
                    WHEN 4  THEN 2    -- Epic
                    WHEN 8  THEN 3    -- Xbox
                    WHEN 16 THEN 4    -- Psn
                    WHEN 32 THEN 5    -- Nintendo
                    WHEN 64 THEN 99   -- Other
                    ELSE "Store"
                END;
                UPDATE "GameStoreOwnedTitles"
                SET "Store" = CASE "Store"
                    WHEN 1  THEN 0    -- Steam
                    WHEN 2  THEN 1    -- Gog
                    WHEN 4  THEN 2    -- Epic
                    WHEN 8  THEN 3    -- Xbox
                    WHEN 16 THEN 4    -- Psn
                    WHEN 32 THEN 5    -- Nintendo
                    WHEN 64 THEN 99   -- Other
                    ELSE "Store"
                END;
                """);
        }
    }
}
