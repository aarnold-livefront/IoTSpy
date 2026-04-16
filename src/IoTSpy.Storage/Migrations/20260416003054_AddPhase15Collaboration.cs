using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSpy.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase15Collaboration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvestigationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedByUsername = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ClosedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShareToken = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestigationSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaptureAnnotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CaptureId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaptureAnnotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaptureAnnotations_InvestigationSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "InvestigationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionActivities_InvestigationSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "InvestigationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionCaptures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CaptureId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AddedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionCaptures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionCaptures_Captures_CaptureId",
                        column: x => x.CaptureId,
                        principalTable: "Captures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionCaptures_InvestigationSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "InvestigationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaptureAnnotations_CaptureId",
                table: "CaptureAnnotations",
                column: "CaptureId");

            migrationBuilder.CreateIndex(
                name: "IX_CaptureAnnotations_SessionId",
                table: "CaptureAnnotations",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CaptureAnnotations_UserId",
                table: "CaptureAnnotations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationSessions_CreatedByUserId",
                table: "InvestigationSessions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationSessions_IsActive",
                table: "InvestigationSessions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationSessions_ShareToken",
                table: "InvestigationSessions",
                column: "ShareToken",
                unique: true,
                filter: "ShareToken IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SessionActivities_SessionId",
                table: "SessionActivities",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionActivities_Timestamp",
                table: "SessionActivities",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCaptures_CaptureId",
                table: "SessionCaptures",
                column: "CaptureId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCaptures_SessionId",
                table: "SessionCaptures",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCaptures_SessionId_CaptureId",
                table: "SessionCaptures",
                columns: new[] { "SessionId", "CaptureId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaptureAnnotations");

            migrationBuilder.DropTable(
                name: "SessionActivities");

            migrationBuilder.DropTable(
                name: "SessionCaptures");

            migrationBuilder.DropTable(
                name: "InvestigationSessions");
        }
    }
}
