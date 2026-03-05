using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSpy.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenRtbInspection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OpenRtbEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CapturedRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: false),
                    MessageType = table.Column<int>(type: "INTEGER", nullable: false),
                    ImpressionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    BidCount = table.Column<int>(type: "INTEGER", nullable: false),
                    HasDeviceInfo = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasUserData = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasGeoData = table.Column<bool>(type: "INTEGER", nullable: false),
                    Exchange = table.Column<string>(type: "TEXT", nullable: false),
                    SeatBids = table.Column<string>(type: "TEXT", nullable: true),
                    RawJson = table.Column<string>(type: "TEXT", nullable: false),
                    DetectedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenRtbEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenRtbPiiPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    FieldPath = table.Column<string>(type: "TEXT", nullable: false),
                    Strategy = table.Column<int>(type: "INTEGER", nullable: false),
                    HostPattern = table.Column<string>(type: "TEXT", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenRtbPiiPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PiiStrippingLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CapturedRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Host = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    FieldPath = table.Column<string>(type: "TEXT", nullable: false),
                    Strategy = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalValueHash = table.Column<string>(type: "TEXT", nullable: false),
                    RedactedPreview = table.Column<string>(type: "TEXT", nullable: false),
                    Phase = table.Column<int>(type: "INTEGER", nullable: false),
                    StrippedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PiiStrippingLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpenRtbEvents_CapturedRequestId",
                table: "OpenRtbEvents",
                column: "CapturedRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenRtbEvents_DetectedAt",
                table: "OpenRtbEvents",
                column: "DetectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OpenRtbEvents_Exchange",
                table: "OpenRtbEvents",
                column: "Exchange");

            migrationBuilder.CreateIndex(
                name: "IX_OpenRtbEvents_MessageType",
                table: "OpenRtbEvents",
                column: "MessageType");

            migrationBuilder.CreateIndex(
                name: "IX_OpenRtbPiiPolicies_Enabled",
                table: "OpenRtbPiiPolicies",
                column: "Enabled");

            migrationBuilder.CreateIndex(
                name: "IX_OpenRtbPiiPolicies_Priority",
                table: "OpenRtbPiiPolicies",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_PiiStrippingLogs_CapturedRequestId",
                table: "PiiStrippingLogs",
                column: "CapturedRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PiiStrippingLogs_FieldPath",
                table: "PiiStrippingLogs",
                column: "FieldPath");

            migrationBuilder.CreateIndex(
                name: "IX_PiiStrippingLogs_Host",
                table: "PiiStrippingLogs",
                column: "Host");

            migrationBuilder.CreateIndex(
                name: "IX_PiiStrippingLogs_StrippedAt",
                table: "PiiStrippingLogs",
                column: "StrippedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpenRtbEvents");

            migrationBuilder.DropTable(
                name: "OpenRtbPiiPolicies");

            migrationBuilder.DropTable(
                name: "PiiStrippingLogs");
        }
    }
}
