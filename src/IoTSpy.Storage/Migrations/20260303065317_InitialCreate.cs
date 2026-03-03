using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSpy.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Certificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CommonName = table.Column<string>(type: "TEXT", nullable: false),
                    SubjectAltNames = table.Column<string>(type: "TEXT", nullable: false),
                    CertificatePem = table.Column<string>(type: "TEXT", nullable: false),
                    PrivateKeyPem = table.Column<string>(type: "TEXT", nullable: false),
                    NotBefore = table.Column<long>(type: "INTEGER", nullable: false),
                    NotAfter = table.Column<long>(type: "INTEGER", nullable: false),
                    SerialNumber = table.Column<string>(type: "TEXT", nullable: false),
                    IsRootCa = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certificates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", nullable: false),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: false),
                    Hostname = table.Column<string>(type: "TEXT", nullable: false),
                    Vendor = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    InterceptionEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirstSeen = table.Column<long>(type: "INTEGER", nullable: false),
                    LastSeen = table.Column<long>(type: "INTEGER", nullable: false),
                    SecurityScore = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProxySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProxyPort = table.Column<int>(type: "INTEGER", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRunning = table.Column<bool>(type: "INTEGER", nullable: false),
                    CaptureTls = table.Column<bool>(type: "INTEGER", nullable: false),
                    CaptureRequestBodies = table.Column<bool>(type: "INTEGER", nullable: false),
                    CaptureResponseBodies = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxBodySizeKb = table.Column<int>(type: "INTEGER", nullable: false),
                    ListenAddress = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProxySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Captures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Method = table.Column<string>(type: "TEXT", nullable: false),
                    Scheme = table.Column<string>(type: "TEXT", nullable: false),
                    Host = table.Column<string>(type: "TEXT", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    Query = table.Column<string>(type: "TEXT", nullable: false),
                    RequestHeaders = table.Column<string>(type: "TEXT", nullable: false),
                    RequestBody = table.Column<string>(type: "TEXT", nullable: false),
                    RequestBodySize = table.Column<long>(type: "INTEGER", nullable: false),
                    StatusCode = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusMessage = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseHeaders = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseBody = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseBodySize = table.Column<long>(type: "INTEGER", nullable: false),
                    IsTls = table.Column<bool>(type: "INTEGER", nullable: false),
                    TlsVersion = table.Column<string>(type: "TEXT", nullable: false),
                    TlsCipherSuite = table.Column<string>(type: "TEXT", nullable: false),
                    Protocol = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                    ClientIp = table.Column<string>(type: "TEXT", nullable: false),
                    IsModified = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Captures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Captures_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Captures_DeviceId",
                table: "Captures",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Captures_Host",
                table: "Captures",
                column: "Host");

            migrationBuilder.CreateIndex(
                name: "IX_Captures_Timestamp",
                table: "Captures",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_CommonName",
                table: "Certificates",
                column: "CommonName");

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_IsRootCa",
                table: "Certificates",
                column: "IsRootCa");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_IpAddress",
                table: "Devices",
                column: "IpAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_MacAddress",
                table: "Devices",
                column: "MacAddress");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Captures");

            migrationBuilder.DropTable(
                name: "Certificates");

            migrationBuilder.DropTable(
                name: "ProxySettings");

            migrationBuilder.DropTable(
                name: "Devices");
        }
    }
}
