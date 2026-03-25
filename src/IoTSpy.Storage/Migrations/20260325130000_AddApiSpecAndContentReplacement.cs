using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSpy.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddApiSpecAndContentReplacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiSpecDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Host = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: false),
                    OpenApiJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    MockEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    PassthroughFirst = table.Column<bool>(type: "INTEGER", nullable: false),
                    UseLlmAnalysis = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiSpecDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentReplacementRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApiSpecDocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MatchType = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchPattern = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    ReplacementValue = table.Column<string>(type: "TEXT", nullable: true),
                    ReplacementFilePath = table.Column<string>(type: "TEXT", nullable: true),
                    ReplacementContentType = table.Column<string>(type: "TEXT", nullable: true),
                    HostPattern = table.Column<string>(type: "TEXT", nullable: true),
                    PathPattern = table.Column<string>(type: "TEXT", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentReplacementRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentReplacementRules_ApiSpecDocuments_ApiSpecDocumentId",
                        column: x => x.ApiSpecDocumentId,
                        principalTable: "ApiSpecDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiSpecDocuments_Host",
                table: "ApiSpecDocuments",
                column: "Host");

            migrationBuilder.CreateIndex(
                name: "IX_ApiSpecDocuments_Status",
                table: "ApiSpecDocuments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReplacementRules_ApiSpecDocumentId",
                table: "ContentReplacementRules",
                column: "ApiSpecDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReplacementRules_Priority",
                table: "ContentReplacementRules",
                column: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ContentReplacementRules");
            migrationBuilder.DropTable(name: "ApiSpecDocuments");
        }
    }
}
