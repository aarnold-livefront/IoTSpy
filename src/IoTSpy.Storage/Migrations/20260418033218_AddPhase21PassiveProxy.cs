using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSpy.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase21PassiveProxy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PassiveCaptureSessionId",
                table: "Captures",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PassiveCaptureSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    EntryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceFilter = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassiveCaptureSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Captures_PassiveCaptureSessionId",
                table: "Captures",
                column: "PassiveCaptureSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PassiveCaptureSessions_CreatedAt",
                table: "PassiveCaptureSessions",
                column: "CreatedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_Captures_PassiveCaptureSessions_PassiveCaptureSessionId",
                table: "Captures",
                column: "PassiveCaptureSessionId",
                principalTable: "PassiveCaptureSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Captures_PassiveCaptureSessions_PassiveCaptureSessionId",
                table: "Captures");

            migrationBuilder.DropTable(
                name: "PassiveCaptureSessions");

            migrationBuilder.DropIndex(
                name: "IX_Captures_PassiveCaptureSessionId",
                table: "Captures");

            migrationBuilder.DropColumn(
                name: "PassiveCaptureSessionId",
                table: "Captures");
        }
    }
}
