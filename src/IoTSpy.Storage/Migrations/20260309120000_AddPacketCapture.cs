using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSpy.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddPacketCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaptureDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", nullable: false),
                    CanCapture = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsPromiscuousMode = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaptureDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Packets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CaptureIndex = table.Column<long>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    TimeDeltaFromPrevious = table.Column<double>(type: "REAL", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Protocol = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Layer2Protocol = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Layer3Protocol = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Layer4Protocol = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SourceIp = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    DestinationIp = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    SourcePort = table.Column<int>(type: "INTEGER", nullable: true),
                    DestinationPort = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceMac = table.Column<string>(type: "TEXT", maxLength: 17, nullable: false),
                    DestinationMac = table.Column<string>(type: "TEXT", maxLength: 17, nullable: false),
                    Length = table.Column<int>(type: "INTEGER", nullable: false),
                    FrameNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    IsError = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRetransmission = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFragment = table.Column<bool>(type: "INTEGER", nullable: false),
                    TcpFlags = table.Column<string>(type: "TEXT", nullable: true),
                    UdpLength = table.Column<string>(type: "TEXT", nullable: true),
                    ArpOperation = table.Column<string>(type: "TEXT", nullable: true),
                    ArpSenderMac = table.Column<string>(type: "TEXT", nullable: true),
                    ArpTargetIp = table.Column<string>(type: "TEXT", nullable: true),
                    DnsQueryName = table.Column<string>(type: "TEXT", nullable: true),
                    DnsResponseType = table.Column<string>(type: "TEXT", nullable: true),
                    HttpMethodName = table.Column<string>(type: "TEXT", nullable: true),
                    HttpRequestUri = table.Column<string>(type: "TEXT", nullable: true),
                    HttpResponseCode = table.Column<int>(type: "INTEGER", nullable: true),
                    PayloadPreview = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    IsSuspicious = table.Column<bool>(type: "INTEGER", nullable: false),
                    SuspicionReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Packets_CaptureDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "CaptureDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaptureDevices_IpAddress",
                table: "CaptureDevices",
                column: "IpAddress");

            migrationBuilder.CreateIndex(
                name: "IX_CaptureDevices_MacAddress",
                table: "CaptureDevices",
                column: "MacAddress");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_DestinationIp",
                table: "Packets",
                column: "DestinationIp");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_DeviceId",
                table: "Packets",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_Protocol",
                table: "Packets",
                column: "Protocol");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_SourceIp",
                table: "Packets",
                column: "SourceIp");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_Timestamp",
                table: "Packets",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Packets");
            migrationBuilder.DropTable(name: "CaptureDevices");
        }
    }
}
