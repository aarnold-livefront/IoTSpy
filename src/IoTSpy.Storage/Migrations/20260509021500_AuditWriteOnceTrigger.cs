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
            //
            // SQLite and Postgres need different DDL: SQLite uses RAISE(ABORT,...)
            // inside a BEFORE-trigger body; Postgres requires a trigger function
            // that RAISEs an exception. The earlier version of this migration
            // emitted only the SQLite form unconditionally, which would fail at
            // migration time on Postgres deployments.
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql("""
                    CREATE TRIGGER IF NOT EXISTS prevent_audit_update
                    BEFORE UPDATE ON AuditEntries
                    BEGIN
                        SELECT RAISE(ABORT, 'AuditEntries are immutable and cannot be modified');
                    END;
                    """);
            }
            else if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("""
                    CREATE OR REPLACE FUNCTION prevent_audit_update_fn()
                    RETURNS trigger AS $$
                    BEGIN
                        RAISE EXCEPTION 'AuditEntries are immutable and cannot be modified';
                    END;
                    $$ LANGUAGE plpgsql;
                    """);
                migrationBuilder.Sql("""
                    DROP TRIGGER IF EXISTS prevent_audit_update ON "AuditEntries";
                    """);
                migrationBuilder.Sql("""
                    CREATE TRIGGER prevent_audit_update
                    BEFORE UPDATE ON "AuditEntries"
                    FOR EACH ROW EXECUTE FUNCTION prevent_audit_update_fn();
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql("DROP TRIGGER IF EXISTS prevent_audit_update;");
            }
            else if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("""DROP TRIGGER IF EXISTS prevent_audit_update ON "AuditEntries";""");
                migrationBuilder.Sql("DROP FUNCTION IF EXISTS prevent_audit_update_fn();");
            }
        }
    }
}
