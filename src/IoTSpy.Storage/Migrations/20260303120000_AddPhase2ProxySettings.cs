using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSpy.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2ProxySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TransparentProxyPort",
                table: "ProxySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 9999);

            migrationBuilder.AddColumn<string>(
                name: "TargetDeviceIp",
                table: "ProxySettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GatewayIp",
                table: "ProxySettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NetworkInterface",
                table: "ProxySettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TransparentProxyPort",
                table: "ProxySettings");

            migrationBuilder.DropColumn(
                name: "TargetDeviceIp",
                table: "ProxySettings");

            migrationBuilder.DropColumn(
                name: "GatewayIp",
                table: "ProxySettings");

            migrationBuilder.DropColumn(
                name: "NetworkInterface",
                table: "ProxySettings");
        }
    }
}
