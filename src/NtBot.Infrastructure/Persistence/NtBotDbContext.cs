using Microsoft.EntityFrameworkCore;
using NtBot.Domain.Entities;
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

            // Seed data (opcional, para desenvolvimento)
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            var planFreeId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var planProId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var planEnterpriseId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
            var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

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
                    Description = "Ilimitado, API access, suporte prioritário",
                    MonthlyPrice = 199.00m,
                    YearlyPrice = 1990.00m,
                    MaxStrategies = 50,
                    MaxBrokers = 10,
                    MaxTradingAccounts = 20,
                    MaxActivePositions = 20,
                    SortOrder = 2,
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
        }
    }
}

