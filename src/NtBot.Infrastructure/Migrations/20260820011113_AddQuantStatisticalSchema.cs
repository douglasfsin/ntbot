using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NtBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantStatisticalSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "quant");

            migrationBuilder.CreateTable(
                name: "asset_correlations",
                schema: "quant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SymbolA = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SymbolB = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Window = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Pearson = table.Column<decimal>(type: "numeric", nullable: true),
                    ReturnA = table.Column<decimal>(type: "numeric", nullable: true),
                    ReturnB = table.Column<decimal>(type: "numeric", nullable: true),
                    VolatilityA = table.Column<decimal>(type: "numeric", nullable: true),
                    VolatilityB = table.Column<decimal>(type: "numeric", nullable: true),
                    Beta = table.Column<decimal>(type: "numeric", nullable: true),
                    SampleCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_correlations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "market_features",
                schema: "quant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Market = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Timeframe = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Open = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    High = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Low = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Close = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Volume = table.Column<long>(type: "bigint", nullable: false),
                    TradeCount = table.Column<int>(type: "integer", nullable: false),
                    BuyVolume = table.Column<long>(type: "bigint", nullable: true),
                    SellVolume = table.Column<long>(type: "bigint", nullable: true),
                    AggressiveBuyVolume = table.Column<long>(type: "bigint", nullable: true),
                    AggressiveSellVolume = table.Column<long>(type: "bigint", nullable: true),
                    Delta = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    CumulativeDelta = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    DeltaRatio = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    BidVolume = table.Column<long>(type: "bigint", nullable: true),
                    AskVolume = table.Column<long>(type: "bigint", nullable: true),
                    BookImbalance = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    Spread = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    Vwap = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    DistanceVwap = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    DistanceVwapAtr = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    Atr = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    Range = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    RangeAtrRatio = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    Volatility = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    AverageTradeSize = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    LargeTradeCount = table.Column<int>(type: "integer", nullable: true),
                    LargeTradeVolume = table.Column<long>(type: "bigint", nullable: true),
                    TradeSizePercentile = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    LargeTradeClass = table.Column<string>(type: "text", nullable: true),
                    VolumeZscore = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    DeltaZscore = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    VolumePercentile = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    DeltaPercentile = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    MarketRegime = table.Column<string>(type: "text", nullable: true),
                    TrendDirection = table.Column<string>(type: "text", nullable: true),
                    TrendStrength = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    MultiTimeframeAlignment = table.Column<string>(type: "text", nullable: true),
                    Trend1m = table.Column<string>(type: "text", nullable: true),
                    Trend5m = table.Column<string>(type: "text", nullable: true),
                    Trend15m = table.Column<string>(type: "text", nullable: true),
                    Trend30m = table.Column<string>(type: "text", nullable: true),
                    Trend60m = table.Column<string>(type: "text", nullable: true),
                    Absorption = table.Column<string>(type: "text", nullable: true),
                    AbsorptionStrength = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    QuantScore = table.Column<int>(type: "integer", nullable: true),
                    TrendScore = table.Column<int>(type: "integer", nullable: true),
                    VolumeScore = table.Column<int>(type: "integer", nullable: true),
                    DeltaScore = table.Column<int>(type: "integer", nullable: true),
                    AggressionScore = table.Column<int>(type: "integer", nullable: true),
                    BookScore = table.Column<int>(type: "integer", nullable: true),
                    VwapScore = table.Column<int>(type: "integer", nullable: true),
                    VolatilityScore = table.Column<int>(type: "integer", nullable: true),
                    AuctionScore = table.Column<int>(type: "integer", nullable: true),
                    MtfScore = table.Column<int>(type: "integer", nullable: true),
                    CrossMarketScore = table.Column<int>(type: "integer", nullable: true),
                    Session = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Source = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_features", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "opening_auction",
                schema: "quant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AuctionStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AuctionEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PreviousClose = table.Column<decimal>(type: "numeric", nullable: true),
                    IndicativePrice = table.Column<decimal>(type: "numeric", nullable: true),
                    EquilibriumPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    OpeningPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    GapPoints = table.Column<decimal>(type: "numeric", nullable: true),
                    GapPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    Volume = table.Column<long>(type: "bigint", nullable: false),
                    BuyVolume = table.Column<long>(type: "bigint", nullable: true),
                    SellVolume = table.Column<long>(type: "bigint", nullable: true),
                    Delta = table.Column<decimal>(type: "numeric", nullable: true),
                    DeltaRatio = table.Column<decimal>(type: "numeric", nullable: true),
                    BidVolume = table.Column<long>(type: "bigint", nullable: true),
                    AskVolume = table.Column<long>(type: "bigint", nullable: true),
                    BookImbalance = table.Column<decimal>(type: "numeric", nullable: true),
                    Spread = table.Column<decimal>(type: "numeric", nullable: true),
                    Vwap = table.Column<decimal>(type: "numeric", nullable: true),
                    AuctionScore = table.Column<int>(type: "integer", nullable: false),
                    AuctionClassification = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    IndicativeDataAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_opening_auction", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "opening_range",
                schema: "quant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RangeWindow = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    RangeStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RangeEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OpeningPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    High = table.Column<decimal>(type: "numeric", nullable: false),
                    Low = table.Column<decimal>(type: "numeric", nullable: false),
                    RangePoints = table.Column<decimal>(type: "numeric", nullable: false),
                    RangePercent = table.Column<decimal>(type: "numeric", nullable: true),
                    Volume = table.Column<long>(type: "bigint", nullable: false),
                    Delta = table.Column<decimal>(type: "numeric", nullable: true),
                    BuyVolume = table.Column<long>(type: "bigint", nullable: true),
                    SellVolume = table.Column<long>(type: "bigint", nullable: true),
                    Vwap = table.Column<decimal>(type: "numeric", nullable: true),
                    BreakoutUp = table.Column<bool>(type: "boolean", nullable: false),
                    BreakoutDown = table.Column<bool>(type: "boolean", nullable: false),
                    BreakoutTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OpeningDrive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_opening_range", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "signal_events",
                schema: "quant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Strategy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Timeframe = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    StopPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    TargetPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    Score = table.Column<decimal>(type: "numeric", nullable: true),
                    Confidence = table.Column<decimal>(type: "numeric", nullable: true),
                    TrendScore = table.Column<int>(type: "integer", nullable: true),
                    VolumeScore = table.Column<int>(type: "integer", nullable: true),
                    DeltaScore = table.Column<int>(type: "integer", nullable: true),
                    AggressionScore = table.Column<int>(type: "integer", nullable: true),
                    BookScore = table.Column<int>(type: "integer", nullable: true),
                    VwapScore = table.Column<int>(type: "integer", nullable: true),
                    VolatilityScore = table.Column<int>(type: "integer", nullable: true),
                    AuctionScore = table.Column<int>(type: "integer", nullable: true),
                    MtfScore = table.Column<int>(type: "integer", nullable: true),
                    CorrelationScore = table.Column<int>(type: "integer", nullable: true),
                    MarketRegime = table.Column<string>(type: "text", nullable: true),
                    Session = table.Column<string>(type: "text", nullable: false),
                    TraceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FeatureId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signal_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "statistical_observations",
                schema: "quant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Strategy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Timeframe = table.Column<string>(type: "text", nullable: false),
                    Session = table.Column<string>(type: "text", nullable: false),
                    MarketRegime = table.Column<string>(type: "text", nullable: false),
                    Direction = table.Column<string>(type: "text", nullable: false),
                    FeatureGroup = table.Column<string>(type: "text", nullable: false),
                    FeatureName = table.Column<string>(type: "text", nullable: false),
                    FeatureValue = table.Column<decimal>(type: "numeric", nullable: true),
                    FeatureBucket = table.Column<string>(type: "text", nullable: false),
                    SampleCount = table.Column<int>(type: "integer", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    SampleClass = table.Column<string>(type: "text", nullable: false),
                    SuccessProbability = table.Column<decimal>(type: "numeric", nullable: true),
                    ConfidenceLow = table.Column<decimal>(type: "numeric", nullable: true),
                    ConfidenceHigh = table.Column<decimal>(type: "numeric", nullable: true),
                    ConfidenceLevel = table.Column<decimal>(type: "numeric", nullable: false),
                    AverageReturn = table.Column<decimal>(type: "numeric", nullable: true),
                    MedianReturn = table.Column<decimal>(type: "numeric", nullable: true),
                    StdReturn = table.Column<decimal>(type: "numeric", nullable: true),
                    MinReturn = table.Column<decimal>(type: "numeric", nullable: true),
                    MaxReturn = table.Column<decimal>(type: "numeric", nullable: true),
                    P25Return = table.Column<decimal>(type: "numeric", nullable: true),
                    P50Return = table.Column<decimal>(type: "numeric", nullable: true),
                    P75Return = table.Column<decimal>(type: "numeric", nullable: true),
                    P90Return = table.Column<decimal>(type: "numeric", nullable: true),
                    P95Return = table.Column<decimal>(type: "numeric", nullable: true),
                    AverageMfe = table.Column<decimal>(type: "numeric", nullable: true),
                    AverageMae = table.Column<decimal>(type: "numeric", nullable: true),
                    AverageWin = table.Column<decimal>(type: "numeric", nullable: true),
                    AverageLoss = table.Column<decimal>(type: "numeric", nullable: true),
                    ProfitFactor = table.Column<decimal>(type: "numeric", nullable: true),
                    Expectancy = table.Column<decimal>(type: "numeric", nullable: true),
                    ExpectancyR = table.Column<decimal>(type: "numeric", nullable: true),
                    SharpeLike = table.Column<decimal>(type: "numeric", nullable: true),
                    SortinoLike = table.Column<decimal>(type: "numeric", nullable: true),
                    MaxDrawdown = table.Column<decimal>(type: "numeric", nullable: true),
                    OutcomeHorizon = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_statistical_observations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "signal_outcomes",
                schema: "quant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SignalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SignalPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Return15s = table.Column<decimal>(type: "numeric", nullable: true),
                    Return30s = table.Column<decimal>(type: "numeric", nullable: true),
                    Return1m = table.Column<decimal>(type: "numeric", nullable: true),
                    Return5m = table.Column<decimal>(type: "numeric", nullable: true),
                    Return15m = table.Column<decimal>(type: "numeric", nullable: true),
                    Return30m = table.Column<decimal>(type: "numeric", nullable: true),
                    Return60m = table.Column<decimal>(type: "numeric", nullable: true),
                    ReturnPoints = table.Column<decimal>(type: "numeric", nullable: true),
                    ReturnPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    ReturnR = table.Column<decimal>(type: "numeric", nullable: true),
                    Mfe15s = table.Column<decimal>(type: "numeric", nullable: true),
                    Mae15s = table.Column<decimal>(type: "numeric", nullable: true),
                    Mfe30s = table.Column<decimal>(type: "numeric", nullable: true),
                    Mae30s = table.Column<decimal>(type: "numeric", nullable: true),
                    Mfe1m = table.Column<decimal>(type: "numeric", nullable: true),
                    Mae1m = table.Column<decimal>(type: "numeric", nullable: true),
                    Mfe5m = table.Column<decimal>(type: "numeric", nullable: true),
                    Mae5m = table.Column<decimal>(type: "numeric", nullable: true),
                    Mfe15m = table.Column<decimal>(type: "numeric", nullable: true),
                    Mae15m = table.Column<decimal>(type: "numeric", nullable: true),
                    Mfe30m = table.Column<decimal>(type: "numeric", nullable: true),
                    Mae30m = table.Column<decimal>(type: "numeric", nullable: true),
                    Mfe60m = table.Column<decimal>(type: "numeric", nullable: true),
                    Mae60m = table.Column<decimal>(type: "numeric", nullable: true),
                    MaxPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    MinPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    TargetHit = table.Column<bool>(type: "boolean", nullable: true),
                    StopHit = table.Column<bool>(type: "boolean", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: true),
                    OutcomeClass = table.Column<string>(type: "text", nullable: true),
                    Complete = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signal_outcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_signal_outcomes_signal_events_SignalId",
                        column: x => x.SignalId,
                        principalSchema: "quant",
                        principalTable: "signal_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_asset_correlations_SymbolA_SymbolB_Window_Timestamp",
                schema: "quant",
                table: "asset_correlations",
                columns: new[] { "SymbolA", "SymbolB", "Window", "Timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_market_features_MarketRegime_Timestamp",
                schema: "quant",
                table: "market_features",
                columns: new[] { "MarketRegime", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_market_features_Session_Timestamp",
                schema: "quant",
                table: "market_features",
                columns: new[] { "Session", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_market_features_Symbol_Timeframe_Timestamp",
                schema: "quant",
                table: "market_features",
                columns: new[] { "Symbol", "Timeframe", "Timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_market_features_Symbol_Timestamp",
                schema: "quant",
                table: "market_features",
                columns: new[] { "Symbol", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_opening_auction_Symbol_Date",
                schema: "quant",
                table: "opening_auction",
                columns: new[] { "Symbol", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_opening_range_Symbol_Date_RangeWindow",
                schema: "quant",
                table: "opening_range",
                columns: new[] { "Symbol", "Date", "RangeWindow" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_signal_events_FeatureId",
                schema: "quant",
                table: "signal_events",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_signal_events_MarketRegime_Timestamp",
                schema: "quant",
                table: "signal_events",
                columns: new[] { "MarketRegime", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_signal_events_Strategy_Timestamp",
                schema: "quant",
                table: "signal_events",
                columns: new[] { "Strategy", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_signal_events_Symbol_Timestamp",
                schema: "quant",
                table: "signal_events",
                columns: new[] { "Symbol", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_signal_events_TraceId",
                schema: "quant",
                table: "signal_events",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_signal_outcomes_SignalId",
                schema: "quant",
                table: "signal_outcomes",
                column: "SignalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_statistical_observations_Strategy_UpdatedAt",
                schema: "quant",
                table: "statistical_observations",
                columns: new[] { "Strategy", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_statistical_observations_Symbol_Strategy_Timeframe_Session_~",
                schema: "quant",
                table: "statistical_observations",
                columns: new[] { "Symbol", "Strategy", "Timeframe", "Session", "MarketRegime", "Direction", "FeatureGroup", "FeatureName", "FeatureBucket", "OutcomeHorizon" },
                unique: true);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_quant_market_features_timestamp_brin
                    ON quant.market_features USING BRIN ("Timestamp");
                CREATE INDEX IF NOT EXISTS ix_quant_signal_events_timestamp_brin
                    ON quant.signal_events USING BRIN ("Timestamp");
                """);

            migrationBuilder.Sql("""
                CREATE MATERIALIZED VIEW IF NOT EXISTS quant.mv_signal_statistics AS
                SELECT
                    s."Symbol",
                    s."Strategy",
                    s."Direction",
                    s."Session",
                    s."MarketRegime",
                    count(*) AS sample_count,
                    count(*) FILTER (WHERE o."Success" = true) AS success_count,
                    avg(o."Return5m") AS average_return,
                    avg(o."Mfe5m") AS average_mfe,
                    avg(o."Mae5m") AS average_mae
                FROM quant.signal_events s
                JOIN quant.signal_outcomes o ON o."SignalId" = s."Id"
                WHERE o."Return5m" IS NOT NULL
                GROUP BY s."Symbol", s."Strategy", s."Direction", s."Session", s."MarketRegime";

                CREATE MATERIALIZED VIEW IF NOT EXISTS quant.mv_opening_statistics AS
                SELECT
                    "Symbol",
                    "AuctionClassification",
                    count(*) AS sample_count,
                    avg("AuctionScore") AS average_score,
                    avg("GapPoints") AS average_gap,
                    avg("Delta") AS average_delta
                FROM quant.opening_auction
                GROUP BY "Symbol", "AuctionClassification";

                CREATE MATERIALIZED VIEW IF NOT EXISTS quant.mv_strategy_performance AS
                SELECT
                    "Symbol",
                    "Strategy",
                    "Direction",
                    "SampleCount",
                    "SampleClass",
                    "SuccessProbability",
                    "ConfidenceLow",
                    "ConfidenceHigh",
                    "Expectancy",
                    "ProfitFactor",
                    "AverageReturn",
                    "MedianReturn",
                    "AverageMfe",
                    "AverageMae",
                    "UpdatedAt"
                FROM quant.statistical_observations
                WHERE "FeatureGroup" = 'delta_zscore';

                CREATE MATERIALIZED VIEW IF NOT EXISTS quant.mv_regime_statistics AS
                SELECT
                    "Symbol",
                    "Strategy",
                    "MarketRegime",
                    sum("SampleCount") AS sample_count,
                    avg("SuccessProbability") AS win_rate,
                    avg("Expectancy") AS expectancy
                FROM quant.statistical_observations
                GROUP BY "Symbol", "Strategy", "MarketRegime";

                CREATE MATERIALIZED VIEW IF NOT EXISTS quant.mv_hourly_statistics AS
                SELECT
                    "Symbol",
                    "Strategy",
                    "Direction",
                    "FeatureBucket" AS hour_bucket,
                    "SampleCount",
                    "SuccessProbability",
                    "Expectancy",
                    "AverageReturn"
                FROM quant.statistical_observations
                WHERE "FeatureGroup" = 'hour';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP MATERIALIZED VIEW IF EXISTS quant.mv_hourly_statistics;
                DROP MATERIALIZED VIEW IF EXISTS quant.mv_regime_statistics;
                DROP MATERIALIZED VIEW IF EXISTS quant.mv_strategy_performance;
                DROP MATERIALIZED VIEW IF EXISTS quant.mv_opening_statistics;
                DROP MATERIALIZED VIEW IF EXISTS quant.mv_signal_statistics;
                DROP INDEX IF EXISTS quant.ix_quant_signal_events_timestamp_brin;
                DROP INDEX IF EXISTS quant.ix_quant_market_features_timestamp_brin;
                """);

            migrationBuilder.DropTable(
                name: "asset_correlations",
                schema: "quant");

            migrationBuilder.DropTable(
                name: "market_features",
                schema: "quant");

            migrationBuilder.DropTable(
                name: "opening_auction",
                schema: "quant");

            migrationBuilder.DropTable(
                name: "opening_range",
                schema: "quant");

            migrationBuilder.DropTable(
                name: "signal_outcomes",
                schema: "quant");

            migrationBuilder.DropTable(
                name: "statistical_observations",
                schema: "quant");

            migrationBuilder.DropTable(
                name: "signal_events",
                schema: "quant");
        }
    }
}
