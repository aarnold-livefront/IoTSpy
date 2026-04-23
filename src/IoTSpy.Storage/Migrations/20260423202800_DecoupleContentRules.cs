using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSpy.Storage.Migrations
{
    /// <inheritdoc />
    public partial class DecoupleContentRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite cannot ALTER COLUMN inside a transaction; rebuild the table manually
            // with the pragma calls marked suppressTransaction so EF doesn't wrap them.
            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);

            migrationBuilder.Sql(@"
                CREATE TABLE ContentReplacementRules_new (
                    Id                    TEXT NOT NULL CONSTRAINT PK_ContentReplacementRules PRIMARY KEY,
                    ApiSpecDocumentId     TEXT NULL,
                    Host                  TEXT NULL,
                    Name                  TEXT NOT NULL,
                    Enabled               INTEGER NOT NULL,
                    MatchType             INTEGER NOT NULL,
                    MatchPattern          TEXT NOT NULL,
                    Action                INTEGER NOT NULL,
                    ReplacementValue      TEXT NULL,
                    ReplacementFilePath   TEXT NULL,
                    ReplacementContentType TEXT NULL,
                    HostPattern           TEXT NULL,
                    PathPattern           TEXT NULL,
                    Priority              INTEGER NOT NULL,
                    CreatedAt             INTEGER NOT NULL,
                    SseInterEventDelayMs  INTEGER NULL,
                    SseLoop               INTEGER NULL,
                    CONSTRAINT FK_ContentReplacementRules_ApiSpecDocuments_ApiSpecDocumentId
                        FOREIGN KEY (ApiSpecDocumentId)
                        REFERENCES ApiSpecDocuments (Id)
                        ON DELETE CASCADE
                );");

            migrationBuilder.Sql(@"
                INSERT INTO ContentReplacementRules_new (
                    Id, ApiSpecDocumentId, Host, Name, Enabled, MatchType, MatchPattern,
                    Action, ReplacementValue, ReplacementFilePath, ReplacementContentType,
                    HostPattern, PathPattern, Priority, CreatedAt, SseInterEventDelayMs, SseLoop
                )
                SELECT
                    Id, ApiSpecDocumentId, NULL, Name, Enabled, MatchType, MatchPattern,
                    Action, ReplacementValue, ReplacementFilePath, ReplacementContentType,
                    HostPattern, PathPattern, Priority, CreatedAt, SseInterEventDelayMs, SseLoop
                FROM ContentReplacementRules;");

            migrationBuilder.Sql("DROP TABLE ContentReplacementRules;");
            migrationBuilder.Sql("ALTER TABLE ContentReplacementRules_new RENAME TO ContentReplacementRules;");

            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentReplacementRules_ApiSpecDocumentId",
                table: "ContentReplacementRules",
                column: "ApiSpecDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReplacementRules_Host",
                table: "ContentReplacementRules",
                column: "Host");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReplacementRules_Priority",
                table: "ContentReplacementRules",
                column: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);

            migrationBuilder.Sql(@"
                CREATE TABLE ContentReplacementRules_old (
                    Id                    TEXT NOT NULL CONSTRAINT PK_ContentReplacementRules PRIMARY KEY,
                    ApiSpecDocumentId     TEXT NOT NULL,
                    Name                  TEXT NOT NULL,
                    Enabled               INTEGER NOT NULL,
                    MatchType             INTEGER NOT NULL,
                    MatchPattern          TEXT NOT NULL,
                    Action                INTEGER NOT NULL,
                    ReplacementValue      TEXT NULL,
                    ReplacementFilePath   TEXT NULL,
                    ReplacementContentType TEXT NULL,
                    HostPattern           TEXT NULL,
                    PathPattern           TEXT NULL,
                    Priority              INTEGER NOT NULL,
                    CreatedAt             INTEGER NOT NULL,
                    SseInterEventDelayMs  INTEGER NULL,
                    SseLoop               INTEGER NULL,
                    CONSTRAINT FK_ContentReplacementRules_ApiSpecDocuments_ApiSpecDocumentId
                        FOREIGN KEY (ApiSpecDocumentId)
                        REFERENCES ApiSpecDocuments (Id)
                        ON DELETE CASCADE
                );");

            migrationBuilder.Sql(@"
                INSERT INTO ContentReplacementRules_old (
                    Id, ApiSpecDocumentId, Name, Enabled, MatchType, MatchPattern,
                    Action, ReplacementValue, ReplacementFilePath, ReplacementContentType,
                    HostPattern, PathPattern, Priority, CreatedAt, SseInterEventDelayMs, SseLoop
                )
                SELECT
                    Id,
                    COALESCE(ApiSpecDocumentId, '00000000-0000-0000-0000-000000000000'),
                    Name, Enabled, MatchType, MatchPattern,
                    Action, ReplacementValue, ReplacementFilePath, ReplacementContentType,
                    HostPattern, PathPattern, Priority, CreatedAt, SseInterEventDelayMs, SseLoop
                FROM ContentReplacementRules;");

            migrationBuilder.Sql("DROP TABLE ContentReplacementRules;");
            migrationBuilder.Sql("ALTER TABLE ContentReplacementRules_old RENAME TO ContentReplacementRules;");

            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentReplacementRules_ApiSpecDocumentId",
                table: "ContentReplacementRules",
                column: "ApiSpecDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReplacementRules_Priority",
                table: "ContentReplacementRules",
                column: "Priority");
        }
    }
}
