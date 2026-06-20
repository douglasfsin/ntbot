using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NtBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Candles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Timeframe = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    OpenTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CloseTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Open = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    High = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Low = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Close = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Volume = table.Column<long>(type: "bigint", nullable: false),
                    Delta = table.Column<long>(type: "bigint", nullable: true),
                    BuyVolume = table.Column<long>(type: "bigint", nullable: true),
                    SellVolume = table.Column<long>(type: "bigint", nullable: true),
                    VWAP = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    POC = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    ATR = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    RSI = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    EMA20 = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    EMA50 = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    EMA200 = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    Time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EconomicEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Impact = table.Column<int>(type: "integer", nullable: false),
                    EventTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Actual = table.Column<string>(type: "text", nullable: true),
                    Forecast = table.Column<string>(type: "text", nullable: true),
                    Previous = table.Column<string>(type: "text", nullable: true),
                    BlockBeforeMinutes = table.Column<int>(type: "integer", nullable: false),
                    BlockAfterMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomicEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentimentScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Sentiment = table.Column<int>(type: "integer", nullable: false),
                    ImpactScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    RelatedSymbols = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: true),
                    Entities = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsAnalyses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderBooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderBooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Plan = table.Column<int>(type: "integer", nullable: false),
                    SubscriptionStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubscriptionEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsTrial = table.Column<bool>(type: "boolean", nullable: false),
                    NinjaTraderApiKey = table.Column<string>(type: "text", nullable: true),
                    NinjaTraderAccountId = table.Column<string>(type: "text", nullable: true),
                    MaxActivePositions = table.Column<int>(type: "integer", nullable: false),
                    MaxDailyTrades = table.Column<int>(type: "integer", nullable: false),
                    MaxRiskPerTrade = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxDailyLoss = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxDrawdownPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TickData",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Bid = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Ask = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Spread = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TickData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderBookLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderBookId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsBid = table.Column<bool>(type: "boolean", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Volume = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    OrdersCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderBookLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderBookLevels_OrderBooks_OrderBookId",
                        column: x => x.OrderBookId,
                        principalTable: "OrderBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Broker = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AccountCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Equity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Margin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FreeMargin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MarginLevel = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DailyProfit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DailyLoss = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Leverage = table.Column<int>(type: "integer", nullable: false),
                    AllowTrading = table.Column<bool>(type: "boolean", nullable: false),
                    AllowExpertAdvisors = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountInfos_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MaxPositionSize = table.Column<int>(type: "integer", nullable: false),
                    RiskPerTrade = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxDailyLoss = table.Column<decimal>(type: "numeric", nullable: false),
                    Timeframes = table.Column<string>(type: "text", nullable: false),
                    MinConfidenceScore = table.Column<decimal>(type: "numeric", nullable: false),
                    MinRiskReward = table.Column<decimal>(type: "numeric", nullable: false),
                    EnableWyckoff = table.Column<bool>(type: "boolean", nullable: false),
                    EnableMacroFilter = table.Column<bool>(type: "boolean", nullable: false),
                    EnableNewsFilter = table.Column<bool>(type: "boolean", nullable: false),
                    EnableEconomicCalendar = table.Column<bool>(type: "boolean", nullable: false),
                    TradingStartTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    TradingEndTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetConfigurations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DailyResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Broker = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    GrossProfit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossLoss = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTrades = table.Column<int>(type: "integer", nullable: false),
                    WinningTrades = table.Column<int>(type: "integer", nullable: false),
                    MaxDrawdown = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxDrawdownPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    MaxExposure = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    AverageExposure = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    AverageLoss = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTradingTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    TotalCommissions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalSwaps = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyResults_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GridOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Broker = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    BasePrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    StepSize = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    MaxLevels = table.Column<int>(type: "integer", nullable: false),
                    LotSize = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    UseMartingale = table.Column<bool>(type: "boolean", nullable: false),
                    MartingaleMultiplier = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ProfitTarget = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    StopLossAmount = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GridOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GridOrders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DailyLossLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DailyProfitTarget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxDailyTrades = table.Column<int>(type: "integer", nullable: false),
                    MaxPositionSize = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    MaxExposure = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    MaxRiskPerTrade = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxRiskPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    MaxDrawdown = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxDrawdownPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    TradingStartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    TradingEndTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    AllowWeekendTrading = table.Column<bool>(type: "boolean", nullable: false),
                    MaxSpread = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    CheckVolatility = table.Column<bool>(type: "boolean", nullable: false),
                    MaxCorrelationExposure = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskConfigs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StrategySignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StrategyName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    StopLoss = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    TakeProfit = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Volume = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsExecuted = table.Column<bool>(type: "boolean", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExecutionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Parameters = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategySignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategySignals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradePositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Broker = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Volume = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    OpenPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    ClosePrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    StopLoss = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    TakeProfit = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    CurrentProfit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Swap = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Commission = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OpenTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CloseTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MagicNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradePositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradePositions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradingSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Broker = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Symbols = table.Column<List<string>>(type: "text[]", nullable: false),
                    Strategies = table.Column<List<string>>(type: "text[]", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TotalTrades = table.Column<int>(type: "integer", nullable: false),
                    TotalVolume = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    GrossProfit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossLoss = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxDrawdown = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxRiskPerTrade = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxDailyLoss = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EnableRiskManagement = table.Column<bool>(type: "boolean", nullable: false),
                    EnableGridTrading = table.Column<bool>(type: "boolean", nullable: false),
                    EnableScalping = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradingSessions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradingSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    WyckoffPhase = table.Column<string>(type: "text", nullable: false),
                    WyckoffEvent = table.Column<string>(type: "text", nullable: false),
                    MacroBias = table.Column<string>(type: "text", nullable: false),
                    NewsImpact = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    EconomicEventActive = table.Column<bool>(type: "boolean", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    StopLoss = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    TakeProfit = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    RiskRewardRatio = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    RiskAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TradeId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingSignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradingSignals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GridLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GridOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Volume = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    IsFilled = table.Column<bool>(type: "boolean", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FilledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OrderId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TradeExecutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Direction = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GridLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GridLevels_GridOrders_GridOrderId",
                        column: x => x.GridOrderId,
                        principalTable: "GridOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradeExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Broker = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Volume = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    StopLoss = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    TakeProfit = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    ExecutionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrderId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Ticket = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TradingSessionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeExecutions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TradeExecutions_TradingSessions_TradingSessionId",
                        column: x => x.TradingSessionId,
                        principalTable: "TradingSessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Trades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignalId = table.Column<Guid>(type: "uuid", nullable: true),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    ExitPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    StopLoss = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    TakeProfit = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    CurrentStopLoss = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    PnL = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PnLPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Commission = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    NetPnL = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    EntryTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExitTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Duration = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExitReason = table.Column<string>(type: "text", nullable: true),
                    MAE = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    MFE = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trades_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Trades_TradingSignals_SignalId",
                        column: x => x.SignalId,
                        principalTable: "TradingSignals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "Tenants",
                columns: new[] { "Id", "CreatedAt", "Email", "IsActive", "IsTrial", "MaxActivePositions", "MaxDailyLoss", "MaxDailyTrades", "MaxDrawdownPercent", "MaxRiskPerTrade", "Name", "NinjaTraderAccountId", "NinjaTraderApiKey", "Plan", "SubscriptionEnd", "SubscriptionStart", "UpdatedAt" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2026, 5, 12, 9, 14, 5, 996, DateTimeKind.Utc).AddTicks(6245), "test@ntbot.com", true, true, 3, 1000m, 20, 5.0m, 2.0m, "Test Tenant", null, null, 1, null, null, null });

            migrationBuilder.InsertData(
                table: "AssetConfigurations",
                columns: new[] { "Id", "CreatedAt", "EnableEconomicCalendar", "EnableMacroFilter", "EnableNewsFilter", "EnableWyckoff", "IsActive", "MaxDailyLoss", "MaxPositionSize", "MinConfidenceScore", "MinRiskReward", "RiskPerTrade", "Symbol", "TenantId", "Timeframes", "TradingEndTime", "TradingStartTime", "UpdatedAt" },
                values: new object[] { new Guid("33333333-3333-3333-3333-333333333333"), new DateTime(2026, 5, 12, 9, 14, 5, 996, DateTimeKind.Utc).AddTicks(7657), true, true, true, true, true, 5.0m, 2, 70.0m, 2.0m, 1.5m, "MNQ", new Guid("11111111-1111-1111-1111-111111111111"), "[\"1m\",\"5m\",\"15m\",\"1h\"]", new TimeSpan(0, 20, 0, 0, 0), new TimeSpan(0, 13, 30, 0, 0), null });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "FullName", "IsActive", "LastLogin", "PasswordHash", "Role", "TenantId", "UpdatedAt" },
                values: new object[] { new Guid("22222222-2222-2222-2222-222222222222"), new DateTime(2026, 5, 12, 9, 14, 5, 996, DateTimeKind.Utc).AddTicks(7532), "admin@ntbot.com", "Admin User", true, null, "$2a$11$XYZ...", 0, new Guid("11111111-1111-1111-1111-111111111111"), null });

            migrationBuilder.CreateIndex(
                name: "IX_AccountInfos_TenantId_Broker_AccountNumber",
                table: "AccountInfos",
                columns: new[] { "TenantId", "Broker", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetConfigurations_TenantId_Symbol",
                table: "AssetConfigurations",
                columns: new[] { "TenantId", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Candles_Symbol_Timeframe_OpenTime",
                table: "Candles",
                columns: new[] { "Symbol", "Timeframe", "OpenTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyResults_TenantId_Date_Broker",
                table: "DailyResults",
                columns: new[] { "TenantId", "Date", "Broker" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EconomicEvents_EventTime",
                table: "EconomicEvents",
                column: "EventTime");

            migrationBuilder.CreateIndex(
                name: "IX_EconomicEvents_EventTime_Impact",
                table: "EconomicEvents",
                columns: new[] { "EventTime", "Impact" });

            migrationBuilder.CreateIndex(
                name: "IX_GridLevels_GridOrderId",
                table: "GridLevels",
                column: "GridOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_GridOrders_TenantId_Symbol_IsActive",
                table: "GridOrders",
                columns: new[] { "TenantId", "Symbol", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsAnalyses_PublishedAt",
                table: "NewsAnalyses",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NewsAnalyses_PublishedAt_ImpactScore",
                table: "NewsAnalyses",
                columns: new[] { "PublishedAt", "ImpactScore" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderBookLevels_OrderBookId",
                table: "OrderBookLevels",
                column: "OrderBookId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderBooks_Symbol_Source_Timestamp",
                table: "OrderBooks",
                columns: new[] { "Symbol", "Source", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskConfigs_TenantId_Symbol",
                table: "RiskConfigs",
                columns: new[] { "TenantId", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StrategySignals_IsExecuted",
                table: "StrategySignals",
                column: "IsExecuted");

            migrationBuilder.CreateIndex(
                name: "IX_StrategySignals_TenantId_StrategyName_Timestamp",
                table: "StrategySignals",
                columns: new[] { "TenantId", "StrategyName", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Email",
                table: "Tenants",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TickData_Symbol_Source_Timestamp",
                table: "TickData",
                columns: new[] { "Symbol", "Source", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_TradeExecutions_Broker",
                table: "TradeExecutions",
                column: "Broker");

            migrationBuilder.CreateIndex(
                name: "IX_TradeExecutions_TenantId_Symbol_ExecutionTime",
                table: "TradeExecutions",
                columns: new[] { "TenantId", "Symbol", "ExecutionTime" });

            migrationBuilder.CreateIndex(
                name: "IX_TradeExecutions_TradingSessionId",
                table: "TradeExecutions",
                column: "TradingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TradePositions_Broker",
                table: "TradePositions",
                column: "Broker");

            migrationBuilder.CreateIndex(
                name: "IX_TradePositions_TenantId_Symbol",
                table: "TradePositions",
                columns: new[] { "TenantId", "Symbol" });

            migrationBuilder.CreateIndex(
                name: "IX_Trades_OrderNumber",
                table: "Trades",
                column: "OrderNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_SignalId",
                table: "Trades",
                column: "SignalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trades_Status",
                table: "Trades",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_TenantId_Symbol_EntryTime",
                table: "Trades",
                columns: new[] { "TenantId", "Symbol", "EntryTime" });

            migrationBuilder.CreateIndex(
                name: "IX_TradingSessions_IsActive",
                table: "TradingSessions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TradingSessions_TenantId_SessionName",
                table: "TradingSessions",
                columns: new[] { "TenantId", "SessionName" });

            migrationBuilder.CreateIndex(
                name: "IX_TradingSignals_Status",
                table: "TradingSignals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TradingSignals_TenantId_Symbol_CreatedAt",
                table: "TradingSignals",
                columns: new[] { "TenantId", "Symbol", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_Email",
                table: "Users",
                columns: new[] { "TenantId", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountInfos");

            migrationBuilder.DropTable(
                name: "AssetConfigurations");

            migrationBuilder.DropTable(
                name: "Candles");

            migrationBuilder.DropTable(
                name: "DailyResults");

            migrationBuilder.DropTable(
                name: "EconomicEvents");

            migrationBuilder.DropTable(
                name: "GridLevels");

            migrationBuilder.DropTable(
                name: "NewsAnalyses");

            migrationBuilder.DropTable(
                name: "OrderBookLevels");

            migrationBuilder.DropTable(
                name: "RiskConfigs");

            migrationBuilder.DropTable(
                name: "StrategySignals");

            migrationBuilder.DropTable(
                name: "TickData");

            migrationBuilder.DropTable(
                name: "TradeExecutions");

            migrationBuilder.DropTable(
                name: "TradePositions");

            migrationBuilder.DropTable(
                name: "Trades");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "GridOrders");

            migrationBuilder.DropTable(
                name: "OrderBooks");

            migrationBuilder.DropTable(
                name: "TradingSessions");

            migrationBuilder.DropTable(
                name: "TradingSignals");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}

