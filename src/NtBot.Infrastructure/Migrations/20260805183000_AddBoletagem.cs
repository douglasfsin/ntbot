using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NtBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBoletagem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BoletaSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SessionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Strategy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DailyProfitTarget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxDrawdownPercent = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    TrailingStopPercent = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false, defaultValue: 0m),
                    LotSize = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    ReferenceBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RealizedPnl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FloatingPnl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PeakEquity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentDrawdownPercent = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    RiskIndication = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ScenarioBias = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScenarioRecommendation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ScenarioConfluenceScore = table.Column<int>(type: "integer", nullable: false),
                    AutomationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AllowNeutralEntries = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    PreferredDirection = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    MetaReached = table.Column<bool>(type: "boolean", nullable: false),
                    DrawdownBreached = table.Column<bool>(type: "boolean", nullable: false),
                    PositionsClosedByRule = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    LastMessage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ResultPercentOfBalance = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoletaSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoletaSessions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoletaOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Direction = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Volume = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    EntryPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    StopLoss = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    TakeProfit = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    PeakFavorablePrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    TrailingStopPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    TargetLevel = table.Column<int>(type: "integer", nullable: false),
                    Strategy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Mt5Ticket = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Mt5OrderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RealizedPnl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Message = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoletaOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoletaOrders_BoletaSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "BoletaSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoletaOrders_SessionId_Status",
                table: "BoletaOrders",
                columns: new[] { "SessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BoletaOrders_TenantId_Symbol_CreatedAt",
                table: "BoletaOrders",
                columns: new[] { "TenantId", "Symbol", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BoletaSessions_TenantId_Status",
                table: "BoletaSessions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BoletaSessions_TenantId_Symbol_SessionDate",
                table: "BoletaSessions",
                columns: new[] { "TenantId", "Symbol", "SessionDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BoletaOrders");
            migrationBuilder.DropTable(name: "BoletaSessions");
        }
    }
}
