using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSpy.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase3Scanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScanJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetIp = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PortRange = table.Column<string>(type: "TEXT", nullable: false),
                    MaxConcurrency = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeoutMs = table.Column<int>(type: "INTEGER", nullable: false),
                    EnableFingerprinting = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableCredentialTest = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableCveLookup = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableConfigAudit = table.Column<bool>(type: "INTEGER", nullable: false),
                    TotalFindings = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanJobs_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScanFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScanJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: true),
                    Protocol = table.Column<string>(type: "TEXT", nullable: true),
                    ServiceName = table.Column<string>(type: "TEXT", nullable: true),
                    Banner = table.Column<string>(type: "TEXT", nullable: true),
                    Cpe = table.Column<string>(type: "TEXT", nullable: true),
                    Username = table.Column<string>(type: "TEXT", nullable: true),
                    Password = table.Column<string>(type: "TEXT", nullable: true),
                    CveId = table.Column<string>(type: "TEXT", nullable: true),
                    CvssScore = table.Column<double>(type: "REAL", nullable: true),
                    CveDescription = table.Column<string>(type: "TEXT", nullable: true),
                    Reference = table.Column<string>(type: "TEXT", nullable: true),
                    FoundAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanFindings_ScanJobs_ScanJobId",
                        column: x => x.ScanJobId,
                        principalTable: "ScanJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScanJobs_DeviceId",
                table: "ScanJobs",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanJobs_Status",
                table: "ScanJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ScanJobs_CreatedAt",
                table: "ScanJobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScanFindings_ScanJobId",
                table: "ScanFindings",
                column: "ScanJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanFindings_Type",
                table: "ScanFindings",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_ScanFindings_Severity",
                table: "ScanFindings",
                column: "Severity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScanFindings");

            migrationBuilder.DropTable(
                name: "ScanJobs");
        }
    }
}
