using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSpy.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddBodyCaptureDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // C# model defaults (= true) mean EF Core always provides these values on insert.
            // A database-level DEFAULT would require a SQLite table rebuild (PRAGMA foreign_keys)
            // which cannot run inside a transaction. Backfill existing rows instead.
            migrationBuilder.Sql(
                "UPDATE ProxySettings SET CaptureRequestBodies = 1 WHERE CaptureRequestBodies = 0;");
            migrationBuilder.Sql(
                "UPDATE ProxySettings SET CaptureResponseBodies = 1 WHERE CaptureResponseBodies = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: cannot restore the absence of a default value without a table rebuild.
        }
    }
}
