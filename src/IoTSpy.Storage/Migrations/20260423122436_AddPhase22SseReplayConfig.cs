using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSpy.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase22SseReplayConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SseInterEventDelayMs",
                table: "ContentReplacementRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SseLoop",
                table: "ContentReplacementRules",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SseInterEventDelayMs",
                table: "ContentReplacementRules");

            migrationBuilder.DropColumn(
                name: "SseLoop",
                table: "ContentReplacementRules");
        }
    }
}
