using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NtBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketIntelligenceProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketIntelligenceProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    RefreshIntervalSeconds = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LastSync = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Capabilities = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketIntelligenceProviders", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "MarketIntelligenceProviders",
                columns: new[] { "Id", "Capabilities", "CreatedAt", "Enabled", "LastSync", "Name", "RefreshIntervalSeconds", "Status", "UpdatedAt" },
                values: new object[] { new Guid("40404040-4040-4040-4040-404040404040"), "[\"commodities\",\"indexes\",\"currencies\",\"treasury\",\"sectors\",\"history\"]", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, null, "Yahoo Finance", 60, "healthy", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_MarketIntelligenceProviders_Name",
                table: "MarketIntelligenceProviders",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketIntelligenceProviders");
        }
    }
}
