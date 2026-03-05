using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSpy.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase4Manipulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManipulationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    HostPattern = table.Column<string>(type: "TEXT", nullable: true),
                    PathPattern = table.Column<string>(type: "TEXT", nullable: true),
                    MethodPattern = table.Column<string>(type: "TEXT", nullable: true),
                    Phase = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    HeaderName = table.Column<string>(type: "TEXT", nullable: true),
                    HeaderValue = table.Column<string>(type: "TEXT", nullable: true),
                    BodyReplace = table.Column<string>(type: "TEXT", nullable: true),
                    BodyReplaceWith = table.Column<string>(type: "TEXT", nullable: true),
                    OverrideStatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    DelayMs = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManipulationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Breakpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Language = table.Column<int>(type: "INTEGER", nullable: false),
                    ScriptCode = table.Column<string>(type: "TEXT", nullable: false),
                    HostPattern = table.Column<string>(type: "TEXT", nullable: true),
                    PathPattern = table.Column<string>(type: "TEXT", nullable: true),
                    Phase = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Breakpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReplaySessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OriginalCaptureId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestMethod = table.Column<string>(type: "TEXT", nullable: false),
                    RequestScheme = table.Column<string>(type: "TEXT", nullable: false),
                    RequestHost = table.Column<string>(type: "TEXT", nullable: false),
                    RequestPort = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestPath = table.Column<string>(type: "TEXT", nullable: false),
                    RequestQuery = table.Column<string>(type: "TEXT", nullable: false),
                    RequestHeaders = table.Column<string>(type: "TEXT", nullable: false),
                    RequestBody = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    ResponseHeaders = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseBody = table.Column<string>(type: "TEXT", nullable: false),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplaySessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReplaySessions_Captures_OriginalCaptureId",
                        column: x => x.OriginalCaptureId,
                        principalTable: "Captures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FuzzerJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BaseCaptureId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Strategy = table.Column<int>(type: "INTEGER", nullable: false),
                    MutationCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ConcurrentRequests = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedMutations = table.Column<int>(type: "INTEGER", nullable: false),
                    Anomalies = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuzzerJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FuzzerJobs_Captures_BaseCaptureId",
                        column: x => x.BaseCaptureId,
                        principalTable: "Captures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FuzzerResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FuzzerJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MutationIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    MutationDescription = table.Column<string>(type: "TEXT", nullable: false),
                    MutatedBody = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "INTEGER", nullable: false),
                    ResponseBody = table.Column<string>(type: "TEXT", nullable: false),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                    IsAnomaly = table.Column<bool>(type: "INTEGER", nullable: false),
                    AnomalyReason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuzzerResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FuzzerResults_FuzzerJobs_FuzzerJobId",
                        column: x => x.FuzzerJobId,
                        principalTable: "FuzzerJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManipulationRules_Enabled",
                table: "ManipulationRules",
                column: "Enabled");

            migrationBuilder.CreateIndex(
                name: "IX_ManipulationRules_Priority",
                table: "ManipulationRules",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_Breakpoints_Enabled",
                table: "Breakpoints",
                column: "Enabled");

            migrationBuilder.CreateIndex(
                name: "IX_ReplaySessions_OriginalCaptureId",
                table: "ReplaySessions",
                column: "OriginalCaptureId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplaySessions_CreatedAt",
                table: "ReplaySessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FuzzerJobs_BaseCaptureId",
                table: "FuzzerJobs",
                column: "BaseCaptureId");

            migrationBuilder.CreateIndex(
                name: "IX_FuzzerJobs_Status",
                table: "FuzzerJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FuzzerResults_FuzzerJobId",
                table: "FuzzerResults",
                column: "FuzzerJobId");

            migrationBuilder.CreateIndex(
                name: "IX_FuzzerResults_IsAnomaly",
                table: "FuzzerResults",
                column: "IsAnomaly");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FuzzerResults");
            migrationBuilder.DropTable(name: "FuzzerJobs");
            migrationBuilder.DropTable(name: "ReplaySessions");
            migrationBuilder.DropTable(name: "Breakpoints");
            migrationBuilder.DropTable(name: "ManipulationRules");
        }
    }
}
