using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collectify.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConvertMacLinuxToPc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Linux (3) is no longer a GamePlatform value (#102): a Linux or
            // Steam-Deck title is the same desktop Pc library, and the "how
            // you play" dimension is IsDigital + DigitalStore, not the
            // platform enum. So rows classified as Linux reclassify as Pc
            // (1); Mac (2) stays its own platform. Pure data change — no
            // schema change. Idempotent: the WHERE clause matches nothing on
            // a second run.
            //
            // Identifiers are double-quoted because this SQL is not
            // provider-translated and Postgres folds unquoted names to
            // lowercase (EF creates "Games"/"Platform" quoted); SQLite
            // also accepts the quoting. See docs/architecture.md:43-45.
            //
            // NOTE: this only runs on the SQLite path. Postgres builds its
            // schema with EnsureCreated() and never replays migrations, so
            // the same 3 -> 1 fix is ALSO applied at startup by
            // GamePlatformBackfill (which runs on both providers). Keeping
            // both means the migration documents the change for the SQLite
            // fast path while the backfill covers Postgres.
            migrationBuilder.Sql(
                "UPDATE \"Games\" SET \"Platform\" = 1 WHERE \"Platform\" = 3;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible without data loss: we cannot tell which of the
            // now-Pc rows were originally Linux vs already-Pc. Down is a
            // no-op; the enum member is gone from the model in this
            // migration's predecessor, so restoring 3 would reference a value
            // the application no longer recognises.
        }
    }
}
