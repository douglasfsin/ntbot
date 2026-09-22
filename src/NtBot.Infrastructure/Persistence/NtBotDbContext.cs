using Microsoft.EntityFrameworkCore;
using NtBot.Domain.Entities;
using NtBot.Domain.Entities.Portfolio;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq;

namespace NtBot.Infrastructure.Persistence
{
    public class NtBotDbContext : DbContext
    {
        public NtBotDbContext(DbContextOptions<NtBotDbContext> options) : base(options)
        {
        }

        // Tables
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<AssetConfiguration> AssetConfigurations { get; set; }
        public DbSet<TradingSignal> TradingSignals { get; set; }
        public DbSet<Trade> Trades { get; set; }
        public DbSet<Candle> Candles { get; set; }
        public DbSet<EconomicEvent> EconomicEvents { get; set; }
        public DbSet<NewsAnalysis> NewsAnalyses { get; set; }

        // New Trading Entities
        public DbSet<TradePosition> TradePositions { get; set; }
        public DbSet<TradeExecution> TradeExecutions { get; set; }
        public DbSet<OrderBook> OrderBooks { get; set; }
        public DbSet<OrderBookLevel> OrderBookLevels { get; set; }
        public DbSet<TickData> TickData { get; set; }
        public DbSet<RiskConfig> RiskConfigs { get; set; }
        public DbSet<GridOrder> GridOrders { get; set; }
        public DbSet<GridLevel> GridLevels { get; set; }
        public DbSet<StrategySignal> StrategySignals { get; set; }
        public DbSet<DailyResult> DailyResults { get; set; }
        public DbSet<AccountInfo> AccountInfos { get; set; }
        public DbSet<TradingSession> TradingSessions { get; set; }

        // Billing
        public DbSet<Plan> Plans { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<BillingHistory> BillingHistories { get; set; }
        public DbSet<WebhookEvent> WebhookEvents { get; set; }
        public DbSet<OtpVerification> OtpVerifications { get; set; }

        // Connector Windows
        public DbSet<ConnectorKey> ConnectorKeys { get; set; }
        public DbSet<ConnectorVersion> ConnectorVersions { get; set; }
        public DbSet<ConnectorSession> ConnectorSessions { get; set; }
        public DbSet<ConnectorLog> ConnectorLogs { get; set; }
        public DbSet<ConnectorDownload> ConnectorDownloads { get; set; }

        // Macro Intelligence
        public DbSet<MacroProvider> MacroProviders { get; set; }

        // Market Intelligence
        public DbSet<MarketIntelligenceProvider> MarketIntelligenceProviders { get; set; }

        // Trading Intelligence
        public DbSet<DriverComposition> DriverCompositions { get; set; }

        // Boletagem
        public DbSet<BoletaSession> BoletaSessions { get; set; }
        public DbSet<BoletaOrder> BoletaOrders { get; set; }

        // White-label + Portfolio / Assessoria
        public DbSet<TenantBranding> TenantBrandings { get; set; }
        public DbSet<ClientInvestorProfile> ClientInvestorProfiles { get; set; }
        public DbSet<GoalPortfolio> GoalPortfolios { get; set; }
        public DbSet<AssetClassNode> AssetClassNodes { get; set; }
        public DbSet<ProductCatalogItem> ProductCatalogItems { get; set; }
        public DbSet<PortfolioPosition> PortfolioPositions { get; set; }
        public DbSet<PortfolioRecommendation> PortfolioRecommendations { get; set; }
        public DbSet<PortfolioReportJob> PortfolioReportJobs { get; set; }
        public DbSet<OpenFinanceConsent> OpenFinanceConsents { get; set; }
        public DbSet<PortfolioValuationSnapshot> PortfolioValuationSnapshots { get; set; }
        public DbSet<PortfolioCashflow> PortfolioCashflows { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Tenant Configuration
            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.HasIndex(e => e.Email).IsUnique();
                
                entity.HasMany(e => e.Users)
                    .WithOne(e => e.Tenant)
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasMany(e => e.AssetConfigurations)
                    .WithOne(e => e.Tenant)
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasMany(e => e.Signals)
                    .WithOne()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasMany(e => e.Trades)
                    .WithOne()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.StripeCustomerId).HasMaxLength(255);
                entity.HasIndex(e => e.StripeCustomerId);

                entity.HasOne(e => e.Subscription)
                    .WithOne(e => e.Tenant)
                    .HasForeignKey<Subscription>(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Branding)
                    .WithOne(e => e.Tenant)
                    .HasForeignKey<TenantBranding>(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // User Configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
            });

            // AssetConfiguration
            modelBuilder.Entity<AssetConfiguration>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
                entity.HasIndex(e => new { e.TenantId, e.Symbol }).IsUnique();
            });

            // TradingSignal
            modelBuilder.Entity<TradingSignal>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
                entity.Property(e => e.ConfidenceScore).HasPrecision(5, 2);
                entity.Property(e => e.EntryPrice).HasPrecision(18, 8);
                entity.Property(e => e.StopLoss).HasPrecision(18, 8);
                entity.Property(e => e.TakeProfit).HasPrecision(18, 8);
                entity.Property(e => e.RiskRewardRatio).HasPrecision(5, 2);
                entity.Property(e => e.RiskAmount).HasPrecision(18, 2);
                entity.Property(e => e.NewsImpact).HasPrecision(5, 2);
                
                entity.HasIndex(e => new { e.TenantId, e.Symbol, e.CreatedAt });
                entity.HasIndex(e => e.Status);
                
                entity.HasOne(e => e.Trade)
                    .WithOne(e => e.Signal)
                    .HasForeignKey<Trade>(e => e.SignalId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Trade
            modelBuilder.Entity<Trade>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
                entity.Property(e => e.OrderNumber).HasMaxLength(100);
                entity.Property(e => e.EntryPrice).HasPrecision(18, 8);
                entity.Property(e => e.ExitPrice).HasPrecision(18, 8);
                entity.Property(e => e.StopLoss).HasPrecision(18, 8);
                entity.Property(e => e.TakeProfit).HasPrecision(18, 8);
                entity.Property(e => e.CurrentStopLoss).HasPrecision(18, 8);
                entity.Property(e => e.PnL).HasPrecision(18, 2);
                entity.Property(e => e.PnLPercent).HasPrecision(5, 2);
                entity.Property(e => e.Commission).HasPrecision(18, 2);
                entity.Property(e => e.NetPnL).HasPrecision(18, 2);
                entity.Property(e => e.MAE).HasPrecision(18, 8);
                entity.Property(e => e.MFE).HasPrecision(18, 8);
                
                entity.HasIndex(e => new { e.TenantId, e.Symbol, e.EntryTime });
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.OrderNumber);
            });

            // Candle
            modelBuilder.Entity<Candle>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Timeframe).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Open).HasPrecision(18, 8);
                entity.Property(e => e.High).HasPrecision(18, 8);
                entity.Property(e => e.Low).HasPrecision(18, 8);
                entity.Property(e => e.Close).HasPrecision(18, 8);
                entity.Property(e => e.VWAP).HasPrecision(18, 8);
                entity.Property(e => e.POC).HasPrecision(18, 8);
                entity.Property(e => e.ATR).HasPrecision(18, 8);
                entity.Property(e => e.RSI).HasPrecision(5, 2);
                entity.Property(e => e.EMA20).HasPrecision(18, 8);
                entity.Property(e => e.EMA50).HasPrecision(18, 8);
                entity.Property(e => e.EMA200).HasPrecision(18, 8);
                
                entity.HasIndex(e => new { e.Symbol, e.Timeframe, e.OpenTime }).IsUnique();
            });

            // EconomicEvent
            modelBuilder.Entity<EconomicEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EventName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Country).HasMaxLength(50);
                entity.Property(e => e.Currency).HasMaxLength(10);
                
                entity.HasIndex(e => e.EventTime);
                entity.HasIndex(e => new { e.EventTime, e.Impact });
            });

            // NewsAnalysis
            modelBuilder.Entity<NewsAnalysis>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Source).HasMaxLength(100);
                entity.Property(e => e.Url).HasMaxLength(1000);
                entity.Property(e => e.SentimentScore).HasPrecision(5, 2);
                entity.Property(e => e.ImpactScore).HasPrecision(5, 2);
                
                entity.HasIndex(e => e.PublishedAt);
                entity.HasIndex(e => new { e.PublishedAt, e.ImpactScore });
            });

            // TradePosition
            modelBuilder.Entity<TradePosition>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Broker).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Volume).HasPrecision(18, 8);
                entity.Property(e => e.OpenPrice).HasPrecision(18, 8);
                entity.Property(e => e.ClosePrice).HasPrecision(18, 8);
                entity.Property(e => e.StopLoss).HasPrecision(18, 8);
                entity.Property(e => e.TakeProfit).HasPrecision(18, 8);
                entity.Property(e => e.CurrentProfit).HasPrecision(18, 2);
                entity.Property(e => e.Swap).HasPrecision(18, 2);
                entity.Property(e => e.Commission).HasPrecision(18, 2);
                entity.Property(e => e.Comment).HasMaxLength(50);
                entity.Property(e => e.MagicNumber).HasMaxLength(50);

                entity.HasIndex(e => new { e.TenantId, e.Symbol});
                entity.HasIndex(e => e.Broker);
            });

            // TradeExecution
            modelBuilder.Entity<TradeExecution>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Broker).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Volume).HasPrecision(18, 8);
                entity.Property(e => e.Price).HasPrecision(18, 8);
                entity.Property(e => e.StopLoss).HasPrecision(18, 8);
                entity.Property(e => e.TakeProfit).HasPrecision(18, 8);
                entity.Property(e => e.Comment).HasMaxLength(100);
                entity.Property(e => e.OrderId).HasMaxLength(50);
                entity.Property(e => e.Ticket).HasMaxLength(50);

                entity.HasIndex(e => new { e.TenantId, e.Symbol, e.ExecutionTime });
                entity.HasIndex(e => e.Broker);
            });

            // OrderBook
            modelBuilder.Entity<OrderBook>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Source).IsRequired().HasMaxLength(10);

                entity.HasIndex(e => new { e.Symbol, e.Source, e.Timestamp });
            });

            // OrderBookLevel
            modelBuilder.Entity<OrderBookLevel>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Price).HasPrecision(18, 8);
                entity.Property(e => e.Volume).HasPrecision(18, 8);

                entity.HasOne(e => e.OrderBook)
                    .WithMany(e => e.Levels)
                    .HasForeignKey(e => e.OrderBookId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // TickData
            modelBuilder.Entity<TickData>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Source).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Bid).HasPrecision(18, 8);
                entity.Property(e => e.Ask).HasPrecision(18, 8);

                entity.HasIndex(e => new { e.Symbol, e.Source, e.Timestamp });
            });

            // RiskConfig
            modelBuilder.Entity<RiskConfig>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
                entity.Property(e => e.DailyLossLimit).HasPrecision(18, 2);
                entity.Property(e => e.DailyProfitTarget).HasPrecision(18, 2);
                entity.Property(e => e.MaxPositionSize).HasPrecision(18, 8);
                entity.Property(e => e.MaxExposure).HasPrecision(18, 8);
                entity.Property(e => e.MaxRiskPerTrade).HasPrecision(18, 2);
                entity.Property(e => e.MaxRiskPercentage).HasPrecision(5, 2);
                entity.Property(e => e.MaxDrawdown).HasPrecision(18, 2);
                entity.Property(e => e.MaxDrawdownPercentage).HasPrecision(5, 2);
                entity.Property(e => e.MaxSpread).HasPrecision(18, 8);
                entity.Property(e => e.MaxCorrelationExposure).HasPrecision(18, 8);

                entity.HasIndex(e => new { e.TenantId, e.Symbol }).IsUnique();
            });

            // GridOrder
            modelBuilder.Entity<GridOrder>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Broker).IsRequired().HasMaxLength(10);
                entity.Property(e => e.BasePrice).HasPrecision(18, 8);
                entity.Property(e => e.StepSize).HasPrecision(18, 8);
                entity.Property(e => e.LotSize).HasPrecision(18, 8);
                entity.Property(e => e.MartingaleMultiplier).HasPrecision(5, 2);
                entity.Property(e => e.ProfitTarget).HasPrecision(18, 8);
                entity.Property(e => e.StopLossAmount).HasPrecision(18, 8);
                entity.Property(e => e.IsClosed).HasDefaultValue(false);
                entity.Property(e => e.CloseReason).HasMaxLength(200);

                entity.HasIndex(e => new { e.TenantId, e.Symbol, e.IsActive });
            });

            // GridLevel
            modelBuilder.Entity<GridLevel>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Price).HasPrecision(18, 8);
                entity.Property(e => e.Volume).HasPrecision(18, 8);
                entity.Property(e => e.OrderId).HasMaxLength(50);

                entity.HasOne(e => e.GridOrder)
                    .WithMany(e => e.Levels)
                    .HasForeignKey(e => e.GridOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // StrategySignal
            modelBuilder.Entity<StrategySignal>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.StrategyName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Confidence).HasPrecision(5, 2);
                entity.Property(e => e.EntryPrice).HasPrecision(18, 8);
                entity.Property(e => e.StopLoss).HasPrecision(18, 8);
                entity.Property(e => e.TakeProfit).HasPrecision(18, 8);
                entity.Property(e => e.Volume).HasPrecision(18, 8);
                entity.Property(e => e.Reason).HasMaxLength(500);
                entity.Property(e => e.ExecutionId).HasMaxLength(100);
                entity.Property(e => e.Parameters)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                        v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, new JsonSerializerOptions()) ?? new Dictionary<string, string>())
                    .HasColumnType("TEXT");

                // Add a value comparer so EF Core can correctly compare dictionary contents
                var parametersComparer = new ValueComparer<Dictionary<string, string>>(
                    (d1, d2) => d1 == d2 || (d1 != null && d2 != null && d1.Count == d2.Count && d1.OrderBy(k => k.Key).SequenceEqual(d2.OrderBy(k => k.Key))),
                    d => d != null ? d.Count.GetHashCode() : 0,
                    d => d == null ? new Dictionary<string, string>() : d.ToDictionary(kv => kv.Key, kv => kv.Value));

                entity.Property(e => e.Parameters).Metadata.SetValueComparer(parametersComparer);

                entity.HasIndex(e => new { e.TenantId, e.StrategyName, e.Timestamp });
                entity.HasIndex(e => e.IsExecuted);
            });

            // DailyResult
            modelBuilder.Entity<DailyResult>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Broker).IsRequired().HasMaxLength(10);
                entity.Property(e => e.GrossProfit).HasPrecision(18, 2);
                entity.Property(e => e.GrossLoss).HasPrecision(18, 2);
                //entity.Property(e => e.WinRate).HasPrecision(5, 2);
                //entity.Property(e => e.TotalAverageWin).HasPrecision(18, 2);
                //entity.Property(e => e.TotalAverageLoss).HasPrecision(18, 2);
                //entity.Property(e => e.ProfitFactor).HasPrecision(5, 2);
                entity.Property(e => e.MaxDrawdown).HasPrecision(18, 2);
                entity.Property(e => e.MaxDrawdownPercentage).HasPrecision(5, 2);
                entity.Property(e => e.MaxExposure).HasPrecision(18, 8);
                entity.Property(e => e.AverageExposure).HasPrecision(18, 8);
                entity.Property(e => e.AverageLoss).HasPrecision(18, 2);
                entity.Property(e => e.TotalCommissions).HasPrecision(18, 2);
                entity.Property(e => e.TotalSwaps).HasPrecision(18, 2);

                entity.HasIndex(e => new { e.TenantId, e.Date, e.Broker }).IsUnique();
            });

            // AccountInfo
            modelBuilder.Entity<AccountInfo>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Broker).IsRequired().HasMaxLength(10);
                entity.Property(e => e.AccountNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.AccountCurrency).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Balance).HasPrecision(18, 2);
                entity.Property(e => e.Equity).HasPrecision(18, 2);
                entity.Property(e => e.Margin).HasPrecision(18, 2);
                entity.Property(e => e.FreeMargin).HasPrecision(18, 2);
                entity.Property(e => e.MarginLevel).HasPrecision(18, 2);
                entity.Property(e => e.DailyProfit).HasPrecision(18, 2);
                entity.Property(e => e.DailyLoss).HasPrecision(18, 2);

                entity.HasIndex(e => new { e.TenantId, e.Broker, e.AccountNumber }).IsUnique();
            });

            // TradingSession
            modelBuilder.Entity<TradingSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SessionName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Broker).IsRequired().HasMaxLength(10);
                entity.Property(e => e.TotalVolume).HasPrecision(18, 8);
                entity.Property(e => e.GrossProfit).HasPrecision(18, 2);
                entity.Property(e => e.GrossLoss).HasPrecision(18, 2);
                entity.Property(e => e.MaxDrawdown).HasPrecision(18, 2);
                entity.Property(e => e.MaxRiskPerTrade).HasPrecision(18, 2);
                entity.Property(e => e.MaxDailyLoss).HasPrecision(18, 2);

                entity.HasIndex(e => new { e.TenantId, e.SessionName });
                entity.HasIndex(e => e.IsActive);
            });

            // Plan
            modelBuilder.Entity<Plan>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Slug).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.Slug).IsUnique();
                entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.MonthlyPrice).HasPrecision(10, 2);
                entity.Property(e => e.YearlyPrice).HasPrecision(10, 2);
                entity.Property(e => e.Currency).HasMaxLength(3);
            });

            // Subscription
            modelBuilder.Entity<Subscription>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TenantId).IsUnique();
                entity.HasIndex(e => new { e.TenantId, e.Status });
                entity.HasIndex(e => e.NextPaymentDate);
                entity.HasIndex(e => e.StripeSubscriptionId);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
                entity.Property(e => e.PaymentStatus).IsRequired().HasMaxLength(20);
                entity.Property(e => e.PaymentGateway).HasMaxLength(20);
                entity.Property(e => e.MonthlyPrice).HasPrecision(10, 2);

                entity.HasOne(e => e.Plan)
                    .WithMany(p => p.Subscriptions)
                    .HasForeignKey(e => e.PlanId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // BillingHistory
            modelBuilder.Entity<BillingHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasPrecision(10, 2);
                entity.Property(e => e.Currency).HasMaxLength(3);
                entity.Property(e => e.Status).HasMaxLength(20);
                entity.HasIndex(e => new { e.TenantId, e.CreatedAt });
                entity.HasIndex(e => e.StripeInvoiceId);

                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Subscription)
                    .WithMany(s => s.BillingHistory)
                    .HasForeignKey(e => e.SubscriptionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // WebhookEvent
            modelBuilder.Entity<WebhookEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Gateway).HasMaxLength(20);
                entity.Property(e => e.EventId).IsRequired().HasMaxLength(255);
                entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Status).HasMaxLength(20);
                entity.HasIndex(e => new { e.Gateway, e.EventId }).IsUnique();
                entity.HasIndex(e => e.Status);
            });

            // OtpVerification
            modelBuilder.Entity<OtpVerification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OtpCode).HasMaxLength(6).IsRequired();
                entity.Property(e => e.VerificationType).HasMaxLength(50).IsRequired();
                entity.HasIndex(e => new { e.UserId, e.VerificationType });
                entity.HasIndex(e => new { e.TenantId, e.VerificationType });
                entity.HasIndex(e => new { e.OtpCode, e.VerificationType, e.ExpiresAt });

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ConnectorKey
            modelBuilder.Entity<ConnectorKey>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
                entity.Property(e => e.KeyPrefix).HasMaxLength(32).IsRequired();
                entity.Property(e => e.KeyHash).HasMaxLength(255).IsRequired();
                entity.Property(e => e.LastUsedIp).HasMaxLength(45);
                entity.HasIndex(e => new { e.TenantId, e.IsActive });
                entity.HasIndex(e => e.KeyPrefix);

                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.RotatedFromKey)
                    .WithMany()
                    .HasForeignKey(e => e.RotatedFromKeyId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ConnectorVersion
            modelBuilder.Entity<ConnectorVersion>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Version).HasMaxLength(32).IsRequired();
                entity.Property(e => e.Channel).HasMaxLength(20).IsRequired();
                entity.Property(e => e.Sha256Hash).HasMaxLength(64).IsRequired();
                entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
                entity.HasIndex(e => new { e.Version, e.Channel }).IsUnique();
                entity.HasIndex(e => e.IsPublished);
            });

            // ConnectorSession
            modelBuilder.Entity<ConnectorSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SessionToken).HasMaxLength(128).IsRequired();
                entity.Property(e => e.ConnectorVersion).HasMaxLength(32).IsRequired();
                entity.Property(e => e.MachineName).HasMaxLength(128).IsRequired();
                entity.Property(e => e.OsVersion).HasMaxLength(128);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
                entity.HasIndex(e => new { e.TenantId, e.Status });
                entity.HasIndex(e => e.LastHeartbeatAt);

                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ConnectorKey)
                    .WithMany(k => k.Sessions)
                    .HasForeignKey(e => e.ConnectorKeyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ConnectorLog
            modelBuilder.Entity<ConnectorLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Level).HasMaxLength(20).IsRequired();
                entity.Property(e => e.Message).HasMaxLength(2000).IsRequired();
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.HasIndex(e => new { e.TenantId, e.CreatedAt });

                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.ConnectorKey)
                    .WithMany(k => k.Logs)
                    .HasForeignKey(e => e.ConnectorKeyId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.ConnectorSession)
                    .WithMany(s => s.Logs)
                    .HasForeignKey(e => e.ConnectorSessionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ConnectorDownload
            modelBuilder.Entity<ConnectorDownload>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(512);
                entity.HasIndex(e => new { e.TenantId, e.DownloadedAt });

                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ConnectorKey)
                    .WithMany(k => k.Downloads)
                    .HasForeignKey(e => e.ConnectorKeyId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ConnectorVersion)
                    .WithMany(v => v.Downloads)
                    .HasForeignKey(e => e.ConnectorVersionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<MacroProvider>(entity =>
            {
                entity.ToTable("MacroProviders");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.Property(e => e.ApiUrl).HasMaxLength(500);
                entity.Property(e => e.ApiKey).HasMaxLength(500);
                entity.Property(e => e.Capabilities).HasColumnType("text");
            });

            modelBuilder.Entity<MarketIntelligenceProvider>(entity =>
            {
                entity.ToTable("MarketIntelligenceProviders");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.Property(e => e.Capabilities).HasColumnType("text");
            });

            modelBuilder.Entity<DriverComposition>(entity =>
            {
                entity.ToTable("DriverCompositions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TargetAsset).IsRequired().HasMaxLength(32);
                entity.Property(e => e.DriverAsset).IsRequired().HasMaxLength(32);
                entity.Property(e => e.Category).HasMaxLength(64);
                entity.Property(e => e.Description).HasMaxLength(256);
                entity.HasIndex(e => new { e.TenantId, e.TargetAsset, e.DriverAsset });
            });

            modelBuilder.Entity<BoletaSession>(entity =>
            {
                entity.ToTable("BoletaSessions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(32);
                entity.Property(e => e.Strategy).IsRequired().HasMaxLength(32);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(24);
                entity.Property(e => e.RiskIndication).HasMaxLength(24);
                entity.Property(e => e.ScenarioBias).HasMaxLength(32);
                entity.Property(e => e.ScenarioRecommendation).HasMaxLength(64);
                entity.Property(e => e.PreferredDirection).HasMaxLength(8);
                entity.Property(e => e.LastMessage).HasMaxLength(512);
                entity.Property(e => e.DailyProfitTarget).HasPrecision(18, 2);
                entity.Property(e => e.MaxDrawdownPercent).HasPrecision(8, 2);
                entity.Property(e => e.TrailingStopPercent).HasPrecision(8, 4);
                entity.Property(e => e.LotSize).HasPrecision(18, 8);
                entity.Property(e => e.ReferenceBalance).HasPrecision(18, 2);
                entity.Property(e => e.RealizedPnl).HasPrecision(18, 2);
                entity.Property(e => e.FloatingPnl).HasPrecision(18, 2);
                entity.Property(e => e.PeakEquity).HasPrecision(18, 2);
                entity.Property(e => e.CurrentDrawdownPercent).HasPrecision(8, 2);
                entity.Property(e => e.ResultPercentOfBalance).HasPrecision(10, 4);
                entity.HasIndex(e => new { e.TenantId, e.Symbol, e.SessionDate });
                entity.HasIndex(e => new { e.TenantId, e.Status });
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.Orders)
                    .WithOne(e => e.Session)
                    .HasForeignKey(e => e.SessionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<BoletaOrder>(entity =>
            {
                entity.ToTable("BoletaOrders");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(32);
                entity.Property(e => e.Direction).IsRequired().HasMaxLength(8);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(24);
                entity.Property(e => e.Strategy).HasMaxLength(32);
                entity.Property(e => e.Mt5Ticket).HasMaxLength(64);
                entity.Property(e => e.Mt5OrderId).HasMaxLength(64);
                entity.Property(e => e.Message).HasMaxLength(512);
                entity.Property(e => e.Volume).HasPrecision(18, 8);
                entity.Property(e => e.EntryPrice).HasPrecision(18, 8);
                entity.Property(e => e.StopLoss).HasPrecision(18, 8);
                entity.Property(e => e.TakeProfit).HasPrecision(18, 8);
                entity.Property(e => e.PeakFavorablePrice).HasPrecision(18, 8);
                entity.Property(e => e.TrailingStopPrice).HasPrecision(18, 8);
                entity.Property(e => e.RealizedPnl).HasPrecision(18, 2);
                entity.HasIndex(e => new { e.SessionId, e.Status });
                entity.HasIndex(e => new { e.TenantId, e.Symbol, e.CreatedAt });
            });

            ConfigureWhiteLabelPortfolio(modelBuilder);

            // Seed data (opcional, para desenvolvimento)
            SeedData(modelBuilder);
        }

        private static void ConfigureWhiteLabelPortfolio(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TenantBranding>(entity =>
            {
                entity.HasKey(e => e.TenantId);
                entity.Property(e => e.AppDisplayName).IsRequired().HasMaxLength(120);
                entity.Property(e => e.LogoUrl).HasMaxLength(512);
                entity.Property(e => e.FaviconUrl).HasMaxLength(512);
                entity.Property(e => e.LoginBackgroundUrl).HasMaxLength(512);
                entity.Property(e => e.PrimaryColor).HasMaxLength(16);
                entity.Property(e => e.SecondaryColor).HasMaxLength(16);
                entity.Property(e => e.BgPrimary).HasMaxLength(16);
                entity.Property(e => e.BgSecondary).HasMaxLength(16);
                entity.Property(e => e.TextPrimary).HasMaxLength(16);
                entity.Property(e => e.LoginHeadline).HasMaxLength(200);
                entity.Property(e => e.LoginSubtext).HasMaxLength(400);
                entity.Property(e => e.SupportEmail).HasMaxLength(255);
                entity.Property(e => e.PublicSlug).IsRequired().HasMaxLength(80);
                entity.HasIndex(e => e.PublicSlug).IsUnique();
                entity.Property(e => e.CustomDomain).HasMaxLength(255);
                entity.HasIndex(e => e.CustomDomain);
            });

            modelBuilder.Entity<ClientInvestorProfile>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.TenantId, e.UserId }).IsUnique();
            });

            modelBuilder.Entity<GoalPortfolio>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ObjectiveName).IsRequired().HasMaxLength(160);
                entity.Property(e => e.BaseCurrency).HasMaxLength(8);
                entity.Property(e => e.AnalysisPrompt).HasMaxLength(4000);
                entity.Property(e => e.LastAnalysisSummary).HasMaxLength(8000);
                entity.Property(e => e.LastAnalysisNextBestAction).HasMaxLength(1000);
                entity.HasIndex(e => new { e.TenantId, e.ClientUserId });
                entity.HasIndex(e => new { e.TenantId, e.AdvisorUserId });
                entity.HasMany(e => e.Positions)
                    .WithOne(e => e.GoalPortfolio)
                    .HasForeignKey(e => e.GoalPortfolioId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.Recommendations)
                    .WithOne(e => e.GoalPortfolio)
                    .HasForeignKey(e => e.GoalPortfolioId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.ValuationSnapshots)
                    .WithOne(e => e.GoalPortfolio)
                    .HasForeignKey(e => e.GoalPortfolioId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.Cashflows)
                    .WithOne(e => e.GoalPortfolio)
                    .HasForeignKey(e => e.GoalPortfolioId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AssetClassNode>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(40);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(120);
                entity.HasIndex(e => new { e.TenantId, e.Code });
                entity.HasOne(e => e.Parent)
                    .WithMany()
                    .HasForeignKey(e => e.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ProductCatalogItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CnpjOrTicker).IsRequired().HasMaxLength(40);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.CreditRating).HasMaxLength(16);
                entity.Property(e => e.OffshoreLimitPct).HasPrecision(8, 4);
                entity.HasIndex(e => new { e.TenantId, e.CnpjOrTicker });
                entity.HasOne(e => e.AssetClassNode)
                    .WithMany()
                    .HasForeignKey(e => e.AssetClassNodeId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<PortfolioValuationSnapshot>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TotalMarketValue).HasPrecision(18, 2);
                entity.Property(e => e.CostBasis).HasPrecision(18, 2);
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.HasIndex(e => new { e.GoalPortfolioId, e.SnapshotDate });
            });

            modelBuilder.Entity<PortfolioCashflow>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.HasIndex(e => new { e.GoalPortfolioId, e.CashflowDate });
            });

            modelBuilder.Entity<PortfolioPosition>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(40);
                entity.Property(e => e.CustodianLabel).HasMaxLength(80);
                entity.Property(e => e.Quantity).HasPrecision(18, 8);
                entity.Property(e => e.AvgPrice).HasPrecision(18, 8);
                entity.Property(e => e.WeightPct).HasPrecision(8, 4);
                entity.Property(e => e.TargetValue).HasPrecision(18, 2);
                entity.Property(e => e.MtmValue).HasPrecision(18, 2);
                entity.HasIndex(e => e.GoalPortfolioId);
                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<PortfolioRecommendation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EstimatedSharpeImpact).HasPrecision(8, 4);
                entity.Property(e => e.Notes).HasMaxLength(2000);
                entity.HasIndex(e => new { e.GoalPortfolioId, e.Status });
                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PortfolioReportJob>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status).HasMaxLength(24);
                entity.Property(e => e.RecipientEmail).HasMaxLength(255);
                entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
                entity.Property(e => e.ReportType).HasMaxLength(32);
                entity.HasIndex(e => new { e.TenantId, e.CreatedAt });
                entity.HasIndex(e => new { e.GoalPortfolioId, e.Version });
            });

            modelBuilder.Entity<OpenFinanceConsent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Provider).HasMaxLength(40);
                entity.Property(e => e.Status).HasMaxLength(24);
                entity.Property(e => e.AuditLogRef).HasMaxLength(120);
                entity.HasIndex(e => new { e.TenantId, e.ClientUserId });
            });
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            var planFreeId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var planProId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var planEnterpriseId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
            var planPartnerId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
            var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Partner FeaturesJson: branding.enabled + portfolio.module (decisão v1).
            const string partnerFeatures =
                "{\"branding.enabled\":true,\"branding.custom_login\":true,\"portfolio.module\":true,\"portfolio.max_clients\":200}";
            const string enterpriseFeatures =
                "{\"branding.enabled\":false,\"portfolio.module\":false}";

            modelBuilder.Entity<Plan>().HasData(
                new Plan
                {
                    Id = planFreeId,
                    Name = "Free",
                    Slug = "free",
                    DisplayName = "Free",
                    Description = "Backtesting básico, 1 ativo",
                    MonthlyPrice = 0,
                    MaxStrategies = 1,
                    MaxBrokers = 1,
                    MaxTradingAccounts = 1,
                    MaxActivePositions = 1,
                    SortOrder = 0,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new Plan
                {
                    Id = planProId,
                    Name = "Pro",
                    Slug = "pro",
                    DisplayName = "Trader Pro",
                    Description = "3 ativos, alertas, backtesting avançado",
                    MonthlyPrice = 49.00m,
                    YearlyPrice = 490.00m,
                    MaxStrategies = 5,
                    MaxBrokers = 2,
                    MaxTradingAccounts = 3,
                    MaxActivePositions = 3,
                    SortOrder = 1,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new Plan
                {
                    Id = planEnterpriseId,
                    Name = "Enterprise",
                    Slug = "enterprise",
                    DisplayName = "Enterprise",
                    Description = "Ilimitado, API access, suporte prioritário. White-label via flag Tenant.WhiteLabelEnabled.",
                    MonthlyPrice = 199.00m,
                    YearlyPrice = 1990.00m,
                    MaxStrategies = 50,
                    MaxBrokers = 10,
                    MaxTradingAccounts = 20,
                    MaxActivePositions = 20,
                    FeaturesJson = enterpriseFeatures,
                    SortOrder = 2,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new Plan
                {
                    Id = planPartnerId,
                    Name = "Partner",
                    Slug = "partner",
                    DisplayName = "Partner White-label",
                    Description = "White-label completo + módulo de assessoria / portfólio",
                    MonthlyPrice = 399.00m,
                    YearlyPrice = 3990.00m,
                    MaxStrategies = 50,
                    MaxBrokers = 10,
                    MaxTradingAccounts = 50,
                    MaxActivePositions = 50,
                    FeaturesJson = partnerFeatures,
                    SortOrder = 3,
                    IsActive = true,
                    CreatedAt = seedDate
                });

            // Tenant de teste
            var testTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            modelBuilder.Entity<Tenant>().HasData(new Tenant
            {
                Id = testTenantId,
                Name = "Test Tenant",
                Email = "test@ntbot.com",
                Plan = SubscriptionPlan.PRO,
                IsActive = true,
                IsTrial = true,
                MaxActivePositions = 3,
                MaxDailyTrades = 20,
                MaxRiskPerTrade = 2.0m,
                CreatedAt = seedDate
            });

            // Usuário admin de teste
            var testUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            modelBuilder.Entity<User>().HasData(new User
            {
                Id = testUserId,
                TenantId = testTenantId,
                Email = "admin@ntbot.com",
                PasswordHash = "$2a$11$XYZ...", // Senha: "password123" (usar bcrypt na real)
                FullName = "Admin User",
                Role = UserRole.ADMIN,
                IsActive = true,
                CreatedAt = seedDate
            });
            var mnqConfigId = Guid.Parse("33333333-3333-3333-3333-333333333333");
            modelBuilder.Entity<AssetConfiguration>().HasData(new AssetConfiguration
            {
                Id = mnqConfigId,
                TenantId = testTenantId,
                Symbol = "MNQ",
                IsActive = true,
                MaxPositionSize = 2,
                RiskPerTrade = 1.5m,
                MaxDailyLoss = 5.0m,
                Timeframes = "[\"1m\",\"5m\",\"15m\",\"1h\"]",
                MinConfidenceScore = 70.0m,
                MinRiskReward = 2.0m,
                EnableWyckoff = true,
                EnableMacroFilter = true,
                EnableNewsFilter = true,
                EnableEconomicCalendar = true,
                TradingStartTime = new TimeSpan(13, 30, 0), // 9:30 AM EST
                TradingEndTime = new TimeSpan(20, 0, 0),    // 4:00 PM EST
                CreatedAt = seedDate
            });

            var connectorVersionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
            modelBuilder.Entity<ConnectorVersion>().HasData(new ConnectorVersion
            {
                Id = connectorVersionId,
                Version = "1.0.0",
                Channel = ConnectorChannels.Stable,
                ReleaseNotes = "Versão inicial do NtBot Connector Windows.",
                Sha256Hash = "0000000000000000000000000000000000000000000000000000000000000000",
                FileName = "NtBot.Connector.Windows-1.0.0.zip",
                FileSizeBytes = 0,
                MinPlan = SubscriptionPlan.FREE,
                IsPublished = true,
                PublishedAt = seedDate,
                CreatedAt = seedDate
            });

            var fredId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
            var mt5CalId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
            var bcbId = Guid.Parse("10101010-1010-1010-1010-101010101010");
            var yahooId = Guid.Parse("20202020-2020-2020-2020-202020202020");
            var mockId = Guid.Parse("30303030-3030-3030-3030-303030303030");

            modelBuilder.Entity<MacroProvider>().HasData(
                new MacroProvider
                {
                    Id = fredId,
                    Name = "FRED",
                    Enabled = true,
                    Priority = 1,
                    ApiUrl = "https://api.stlouisfed.org/fred",
                    RefreshIntervalMinutes = 30,
                    Status = "healthy",
                    Capabilities = "[\"rates\",\"inflation\",\"volatility\",\"employment\",\"liquidity\"]",
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                },
                new MacroProvider
                {
                    Id = mt5CalId,
                    Name = "MT5 Economic Calendar",
                    Enabled = true,
                    Priority = 2,
                    RefreshIntervalMinutes = 5,
                    Status = "healthy",
                    Capabilities = "[\"calendar\",\"events\"]",
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                },
                new MacroProvider
                {
                    Id = bcbId,
                    Name = "Banco Central",
                    Enabled = false,
                    Priority = 3,
                    RefreshIntervalMinutes = 30,
                    Status = "disabled",
                    Capabilities = "[\"rates\",\"policy\",\"fx\"]",
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                },
                new MacroProvider
                {
                    Id = yahooId,
                    Name = "Yahoo Finance",
                    Enabled = false,
                    Priority = 4,
                    RefreshIntervalMinutes = 15,
                    Status = "disabled",
                    Capabilities = "[\"fx\",\"equities\",\"commodities\"]",
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                },
                new MacroProvider
                {
                    Id = mockId,
                    Name = "Mock",
                    Enabled = false,
                    Priority = 99,
                    RefreshIntervalMinutes = 5,
                    Status = "disabled",
                    Capabilities = "[\"demo\"]",
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                });

            var yahooMarketId = Guid.Parse("40404040-4040-4040-4040-404040404040");
            modelBuilder.Entity<MarketIntelligenceProvider>().HasData(
                new MarketIntelligenceProvider
                {
                    Id = yahooMarketId,
                    Name = "Yahoo Finance",
                    Enabled = true,
                    RefreshIntervalSeconds = 60,
                    Status = "healthy",
                    Capabilities = "[\"commodities\",\"indexes\",\"currencies\",\"treasury\",\"sectors\",\"history\"]",
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                });

            SeedDriverCompositions(modelBuilder, seedDate);
            SeedPortfolioCatalog(modelBuilder);
        }

        private static void SeedPortfolioCatalog(ModelBuilder modelBuilder)
        {
            var rfId = Guid.Parse("a1000001-0000-0000-0000-000000000001");
            var rvId = Guid.Parse("a1000001-0000-0000-0000-000000000002");
            var fundosId = Guid.Parse("a1000001-0000-0000-0000-000000000003");
            var caixaId = Guid.Parse("a1000001-0000-0000-0000-000000000004");
            var fifSubId = Guid.Parse("a1000001-0000-0000-0000-000000000011");
            var fiiSubId = Guid.Parse("a1000001-0000-0000-0000-000000000012");
            var fidcSubId = Guid.Parse("a1000001-0000-0000-0000-000000000013");
            var fiagroSubId = Guid.Parse("a1000001-0000-0000-0000-000000000014");
            var fipSubId = Guid.Parse("a1000001-0000-0000-0000-000000000015");

            modelBuilder.Entity<AssetClassNode>().HasData(
                new AssetClassNode { Id = rfId, Code = "RF", Name = "Renda Fixa", SortOrder = 1 },
                new AssetClassNode { Id = rvId, Code = "RV", Name = "Renda Variável", SortOrder = 2 },
                new AssetClassNode { Id = fundosId, Code = "FUNDOS", Name = "Fundos Estruturados", SortOrder = 3 },
                new AssetClassNode { Id = caixaId, Code = "CAIXA", Name = "Caixa", SortOrder = 4 },
                new AssetClassNode { Id = fifSubId, Code = "FIF", Name = "FIF — Fundos Financeiros", ParentId = fundosId, SortOrder = 1 },
                new AssetClassNode { Id = fiiSubId, Code = "FII", Name = "FII — Imobiliário", ParentId = fundosId, SortOrder = 2 },
                new AssetClassNode { Id = fidcSubId, Code = "FIDC", Name = "FIDC — Direitos Creditórios", ParentId = fundosId, SortOrder = 3 },
                new AssetClassNode { Id = fiagroSubId, Code = "FIAGRO", Name = "FIAGRO — Agro", ParentId = fundosId, SortOrder = 4 },
                new AssetClassNode { Id = fipSubId, Code = "FIP", Name = "FIP — Participações", ParentId = fundosId, SortOrder = 5 }
            );

            modelBuilder.Entity<ProductCatalogItem>().HasData(
                new ProductCatalogItem
                {
                    Id = Guid.Parse("b1000001-0000-0000-0000-000000000001"),
                    CnpjOrTicker = "00.000.000/0001-01",
                    Name = "FIF Liquidez Conservador",
                    AssetClassNodeId = fifSubId,
                    ProductFamily = ProductFamily.FIF,
                    FlagRetailAllowed = true,
                    CreditRating = "AAA",
                    RiskRating = 1
                },
                new ProductCatalogItem
                {
                    Id = Guid.Parse("b1000001-0000-0000-0000-000000000002"),
                    CnpjOrTicker = "HGLG11",
                    Name = "FII Logística Exemplo",
                    AssetClassNodeId = fiiSubId,
                    ProductFamily = ProductFamily.FII,
                    FlagRetailAllowed = true,
                    RiskRating = 3
                },
                new ProductCatalogItem
                {
                    Id = Guid.Parse("b1000001-0000-0000-0000-000000000003"),
                    CnpjOrTicker = "00.000.000/0001-99",
                    Name = "FIDC Sênior AAA",
                    AssetClassNodeId = fidcSubId,
                    ProductFamily = ProductFamily.FIDC,
                    FlagRetailAllowed = true,
                    CreditRating = "AAA",
                    RiskRating = 2,
                    MetadataJson = "{\"tranche\":\"senior\",\"max_redeem_days\":90}"
                },
                new ProductCatalogItem
                {
                    Id = Guid.Parse("b1000001-0000-0000-0000-000000000004"),
                    CnpjOrTicker = "00.000.000/0001-88",
                    Name = "FIDC Subordinado (Qualificado)",
                    AssetClassNodeId = fidcSubId,
                    ProductFamily = ProductFamily.FIDC,
                    FlagRetailAllowed = false,
                    CreditRating = "BB",
                    RiskRating = 5,
                    MetadataJson = "{\"tranche\":\"subordinated\"}"
                },
                new ProductCatalogItem
                {
                    Id = Guid.Parse("b1000001-0000-0000-0000-00000000000b"),
                    CnpjOrTicker = "00.000.000/0001-55",
                    Name = "FIAGRO Crédito Exemplo",
                    AssetClassNodeId = fiagroSubId,
                    ProductFamily = ProductFamily.FIAGRO,
                    FlagRetailAllowed = true,
                    CreditRating = "A",
                    RiskRating = 3,
                    MetadataJson = "{\"cvm175\":\"fiagro\"}"
                },
                new ProductCatalogItem
                {
                    Id = Guid.Parse("b1000001-0000-0000-0000-00000000000d"),
                    CnpjOrTicker = "00.000.000/0001-33",
                    Name = "FIP Private Equity (Qualificado)",
                    AssetClassNodeId = fipSubId,
                    ProductFamily = ProductFamily.FIP,
                    FlagRetailAllowed = false,
                    RiskRating = 5,
                    OffshoreLimitPct = 20,
                    MetadataJson = "{\"cvm175\":\"fip\",\"segment\":\"qualified\"}"
                }
            );
        }

        private static void SeedDriverCompositions(ModelBuilder modelBuilder, DateTime seedDate)
        {
            var winDrivers = new (Guid id, string asset, decimal weight, int order, string category, string desc)[]
            {
                (Guid.Parse("d1000001-0000-0000-0000-000000000001"), "PETR4", 0.18m, 1, "Correlacao", "PETR4"),
                (Guid.Parse("d1000001-0000-0000-0000-000000000002"), "VALE3", 0.12m, 2, "Correlacao", "VALE3"),
                (Guid.Parse("d1000001-0000-0000-0000-000000000003"), "ITUB4", 0.08m, 3, "Correlacao", "ITUB4"),
                (Guid.Parse("d1000001-0000-0000-0000-000000000004"), "BBDC4", 0.05m, 4, "Correlacao", "BBDC4"),
                (Guid.Parse("d1000001-0000-0000-0000-000000000005"), "WEGE3", 0.04m, 5, "Correlacao", "WEGE3"),
                (Guid.Parse("d1000001-0000-0000-0000-000000000006"), "ABEV3", 0.03m, 6, "Correlacao", "ABEV3"),
                (Guid.Parse("d1000001-0000-0000-0000-000000000007"), "MACRO", 0.50m, 7, "Macro", "Macro")
            };

            foreach (var d in winDrivers)
            {
                modelBuilder.Entity<DriverComposition>().HasData(new DriverComposition
                {
                    Id = d.id,
                    TargetAsset = "WIN",
                    DriverAsset = d.asset,
                    Weight = d.weight,
                    Enabled = true,
                    DisplayOrder = d.order,
                    Description = d.desc,
                    Category = d.category,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                });
            }
        }
    }
}

