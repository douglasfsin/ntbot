using Microsoft.EntityFrameworkCore;
using NtBot.Domain.Entities.Quant;

namespace NtBot.Infrastructure.Persistence;

public static class QuantModelConfiguration
{
    public const string Schema = "quant";

    public static void Apply(ModelBuilder modelBuilder, string? providerName)
    {
        var npgsql = providerName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

        Map<QuantMarketFeature>(modelBuilder, npgsql, "market_features", entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Market).HasMaxLength(32);
            entity.Property(e => e.Timeframe).IsRequired().HasMaxLength(16);
            entity.Property(e => e.Session).HasMaxLength(24);
            entity.HasIndex(e => new { e.Symbol, e.Timestamp });
            entity.HasIndex(e => new { e.Symbol, e.Timeframe, e.Timestamp }).IsUnique();
            entity.HasIndex(e => new { e.MarketRegime, e.Timestamp });
            entity.HasIndex(e => new { e.Session, e.Timestamp });
            PricePrecision(entity);
        });

        Map<QuantOpeningAuction>(modelBuilder, npgsql, "opening_auction", entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(32);
            entity.Property(e => e.AuctionClassification).HasMaxLength(24);
            entity.HasIndex(e => new { e.Symbol, e.Date }).IsUnique();
        });

        Map<QuantOpeningRange>(modelBuilder, npgsql, "opening_range", entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(32);
            entity.Property(e => e.RangeWindow).IsRequired().HasMaxLength(8);
            entity.HasIndex(e => new { e.Symbol, e.Date, e.RangeWindow }).IsUnique();
        });

        Map<QuantSignalEvent>(modelBuilder, npgsql, "signal_events", entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Strategy).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Timeframe).HasMaxLength(16);
            entity.Property(e => e.Direction).IsRequired().HasMaxLength(16);
            entity.Property(e => e.TraceId).HasMaxLength(64);
            entity.HasIndex(e => new { e.Symbol, e.Timestamp });
            entity.HasIndex(e => new { e.Strategy, e.Timestamp });
            entity.HasIndex(e => new { e.MarketRegime, e.Timestamp });
            entity.HasIndex(e => e.TraceId);
            entity.HasIndex(e => e.FeatureId);
            entity.HasOne(e => e.Outcome)
                .WithOne(o => o.Signal)
                .HasForeignKey<QuantSignalOutcome>(o => o.SignalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        Map<QuantSignalOutcome>(modelBuilder, npgsql, "signal_outcomes", entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Direction).HasMaxLength(16);
            entity.HasIndex(e => e.SignalId).IsUnique();
        });

        Map<QuantStatisticalObservation>(modelBuilder, npgsql, "statistical_observations", entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Strategy).IsRequired().HasMaxLength(64);
            entity.HasIndex(e => new
            {
                e.Symbol,
                e.Strategy,
                e.Timeframe,
                e.Session,
                e.MarketRegime,
                e.Direction,
                e.FeatureGroup,
                e.FeatureName,
                e.FeatureBucket,
                e.OutcomeHorizon
            }).IsUnique();
            entity.HasIndex(e => new { e.Strategy, e.UpdatedAt });
        });

        Map<QuantAssetCorrelation>(modelBuilder, npgsql, "asset_correlations", entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SymbolA).IsRequired().HasMaxLength(32);
            entity.Property(e => e.SymbolB).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Window).IsRequired().HasMaxLength(8);
            entity.HasIndex(e => new { e.SymbolA, e.SymbolB, e.Window, e.Timestamp }).IsUnique();
        });
    }

    private static void Map<T>(
        ModelBuilder modelBuilder,
        bool npgsql,
        string table,
        Action<Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T>> configure)
        where T : class
    {
        var entity = modelBuilder.Entity<T>();
        if (npgsql)
            entity.ToTable(table, Schema);
        else
            entity.ToTable($"quant_{table}");
        configure(entity);
    }

    private static void PricePrecision<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> entity)
        where T : class
    {
        foreach (var property in entity.Metadata.GetProperties().Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            property.SetPrecision(18);
        foreach (var property in entity.Metadata.GetProperties().Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            property.SetScale(8);
    }
}
