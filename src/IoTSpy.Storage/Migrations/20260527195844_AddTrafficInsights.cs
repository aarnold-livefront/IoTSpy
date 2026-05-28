using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSpy.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddTrafficInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrafficInsights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CaptureId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ConfidenceJson = table.Column<string>(type: "TEXT", nullable: false),
                    RiskScore = table.Column<double>(type: "REAL", nullable: false),
                    ModelVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsReviewed = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDismissed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReviewNote = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReviewedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrafficInsights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrafficInsights_Captures_CaptureId",
                        column: x => x.CaptureId,
                        principalTable: "Captures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrafficInsights_CaptureId",
                table: "TrafficInsights",
                column: "CaptureId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrafficInsights_CreatedAt",
                table: "TrafficInsights",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TrafficInsights_IsReviewed",
                table: "TrafficInsights",
                column: "IsReviewed");

            migrationBuilder.CreateIndex(
                name: "IX_TrafficInsights_RiskScore",
                table: "TrafficInsights",
                column: "RiskScore");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrafficInsights");
        }
    }
}
