using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSpy.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddCaCustomizationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CaCommonName",
                table: "ProxySettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CaCountry",
                table: "ProxySettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CaOrganization",
                table: "ProxySettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CaValidityYears",
                table: "ProxySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaCommonName",
                table: "ProxySettings");

            migrationBuilder.DropColumn(
                name: "CaCountry",
                table: "ProxySettings");

            migrationBuilder.DropColumn(
                name: "CaOrganization",
                table: "ProxySettings");

            migrationBuilder.DropColumn(
                name: "CaValidityYears",
                table: "ProxySettings");
        }
    }
}
