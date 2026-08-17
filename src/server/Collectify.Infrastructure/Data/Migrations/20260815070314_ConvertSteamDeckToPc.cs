using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collectify.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConvertSteamDeckToPc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Steam Deck is no longer a GamePlatform value: it is a PC
            // (SteamOS/Linux or Windows, runs desktop-PC games), so rows
            // classified as SteamDeck (60) reclassify as Pc (1). The
            // "how you got it" dimension is IsDigital + DigitalStore
            // (Steam), not the platform. Pure data change -- no schema
            // change. Idempotent: the WHERE clause matches nothing on a
            // second run.
            //
            // Identifiers are double-quoted because this SQL is not
            // provider-translated and Postgres folds unquoted names to
            // lowercase (EF creates "Games"/"Platform" quoted); SQLite
            // also accepts the quoting. See docs/architecture.md:43-45.
            //
            // NOTE: this only runs on the SQLite path. Postgres builds its
            // schema with EnsureCreated() and never replays migrations, so
            // the same 60 -> 1 fix is ALSO applied at startup by
            // GamePlatformBackfill (which runs on both providers). Keeping
            // both means the migration documents the change for the SQLite
            // fast path while the backfill covers Postgres.
            migrationBuilder.Sql(
                "UPDATE \"Games\" SET \"Platform\" = 1 WHERE \"Platform\" = 60;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible without data loss: we cannot tell which of the
            // now-Pc rows were originally SteamDeck. Down is a no-op; the
            // enum member is gone from the model in this migration's
            // predecessor, so restoring 60 would reference a value the
            // application no longer recognises.
        }
    }
}
