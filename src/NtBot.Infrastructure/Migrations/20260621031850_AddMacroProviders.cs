using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NtBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMacroProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MacroProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ApiUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApiKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RefreshIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LastSync = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Capabilities = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MacroProviders", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "MacroProviders",
                columns: new[] { "Id", "ApiKey", "ApiUrl", "Capabilities", "CreatedAt", "Enabled", "LastSync", "Name", "Priority", "RefreshIntervalMinutes", "Status", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("10101010-1010-1010-1010-101010101010"), null, null, "[\"rates\",\"policy\",\"fx\"]", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Banco Central", 3, 30, "disabled", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20202020-2020-2020-2020-202020202020"), null, null, "[\"fx\",\"equities\",\"commodities\"]", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Yahoo Finance", 4, 15, "disabled", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30303030-3030-3030-3030-303030303030"), null, null, "[\"demo\"]", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Mock", 99, 5, "disabled", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), null, "https://api.stlouisfed.org/fred", "[\"rates\",\"inflation\",\"volatility\",\"employment\",\"liquidity\"]", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, null, "FRED", 1, 30, "healthy", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"), null, null, "[\"calendar\",\"events\"]", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, null, "MT5 Economic Calendar", 2, 5, "healthy", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_MacroProviders_Name",
                table: "MacroProviders",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MacroProviders");
        }
    }
}
