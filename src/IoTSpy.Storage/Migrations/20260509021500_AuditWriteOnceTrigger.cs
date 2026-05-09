using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSpy.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AuditWriteOnceTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Prevent modification of existing audit entries (immutability by design).
            // Deletions are still permitted for data-retention operations.
            migrationBuilder.Sql("""
                CREATE TRIGGER IF NOT EXISTS prevent_audit_update
                BEFORE UPDATE ON AuditEntries
                BEGIN
                    SELECT RAISE(ABORT, 'AuditEntries are immutable and cannot be modified');
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS prevent_audit_update;");
        }
    }
}
