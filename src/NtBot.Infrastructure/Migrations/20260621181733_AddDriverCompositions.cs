using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NtBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverCompositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DriverCompositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetAsset = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DriverAsset = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Inverse = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverCompositions", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "DriverCompositions",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "DisplayOrder", "DriverAsset", "Enabled", "Inverse", "TargetAsset", "TenantId", "UpdatedAt", "Weight" },
                values: new object[,]
                {
                    { new Guid("d1000001-0000-0000-0000-000000000001"), "Correlacao", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "PETR4", 1, "PETR4", true, false, "WIN", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.18m },
                    { new Guid("d1000001-0000-0000-0000-000000000002"), "Correlacao", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VALE3", 2, "VALE3", true, false, "WIN", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.12m },
                    { new Guid("d1000001-0000-0000-0000-000000000003"), "Correlacao", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ITUB4", 3, "ITUB4", true, false, "WIN", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.08m },
                    { new Guid("d1000001-0000-0000-0000-000000000004"), "Correlacao", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "BBDC4", 4, "BBDC4", true, false, "WIN", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.05m },
                    { new Guid("d1000001-0000-0000-0000-000000000005"), "Correlacao", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "WEGE3", 5, "WEGE3", true, false, "WIN", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.04m },
                    { new Guid("d1000001-0000-0000-0000-000000000006"), "Correlacao", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ABEV3", 6, "ABEV3", true, false, "WIN", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.03m },
                    { new Guid("d1000001-0000-0000-0000-000000000007"), "Macro", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Macro", 7, "MACRO", true, false, "WIN", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.50m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriverCompositions_TenantId_TargetAsset_DriverAsset",
                table: "DriverCompositions",
                columns: new[] { "TenantId", "TargetAsset", "DriverAsset" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverCompositions");
        }
    }
}
