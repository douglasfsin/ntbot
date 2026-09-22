using Microsoft.EntityFrameworkCore;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Api.Services.Boletagem;

/// <summary>
/// Garante tabelas de boletagem quando a migration EF ainda não foi aplicada
/// (ex.: snapshot desatualizado / build travado). Idempotente no PostgreSQL.
/// </summary>
public static class BoletagemSchemaBootstrap
{
    public static async Task EnsureAsync(NtBotDbContext db, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "BoletaSessions" (
                    "Id" uuid NOT NULL,
                    "TenantId" uuid NOT NULL,
                    "Symbol" character varying(32) NOT NULL,
                    "SessionDate" timestamp with time zone NOT NULL,
                    "Strategy" character varying(32) NOT NULL,
                    "DailyProfitTarget" numeric(18,2) NOT NULL DEFAULT 0,
                    "MaxDrawdownPercent" numeric(8,2) NOT NULL DEFAULT 2,
                    "TrailingStopPercent" numeric(8,4) NOT NULL DEFAULT 0,
                    "LotSize" numeric(18,8) NOT NULL DEFAULT 0.01,
                    "ReferenceBalance" numeric(18,2) NOT NULL DEFAULT 0,
                    "RealizedPnl" numeric(18,2) NOT NULL DEFAULT 0,
                    "FloatingPnl" numeric(18,2) NOT NULL DEFAULT 0,
                    "PeakEquity" numeric(18,2) NOT NULL DEFAULT 0,
                    "CurrentDrawdownPercent" numeric(8,2) NOT NULL DEFAULT 0,
                    "RiskIndication" character varying(24) NOT NULL DEFAULT 'Moderado',
                    "ScenarioBias" character varying(32) NOT NULL DEFAULT 'Sideways',
                    "ScenarioRecommendation" character varying(64) NOT NULL DEFAULT 'NEUTRO',
                    "ScenarioConfluenceScore" integer NOT NULL DEFAULT 0,
                    "AutomationEnabled" boolean NOT NULL DEFAULT FALSE,
                    "MetaReached" boolean NOT NULL DEFAULT FALSE,
                    "DrawdownBreached" boolean NOT NULL DEFAULT FALSE,
                    "PositionsClosedByRule" boolean NOT NULL DEFAULT FALSE,
                    "Status" character varying(24) NOT NULL DEFAULT 'Active',
                    "LastMessage" character varying(512) NULL,
                    "ResultPercentOfBalance" numeric(10,4) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_BoletaSessions" PRIMARY KEY ("Id")
                );
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "BoletaOrders" (
                    "Id" uuid NOT NULL,
                    "SessionId" uuid NOT NULL,
                    "TenantId" uuid NOT NULL,
                    "Symbol" character varying(32) NOT NULL,
                    "Direction" character varying(8) NOT NULL,
                    "Volume" numeric(18,8) NOT NULL,
                    "EntryPrice" numeric(18,8) NULL,
                    "StopLoss" numeric(18,8) NULL,
                    "TakeProfit" numeric(18,8) NULL,
                    "PeakFavorablePrice" numeric(18,8) NULL,
                    "TrailingStopPrice" numeric(18,8) NULL,
                    "TargetLevel" integer NOT NULL DEFAULT 0,
                    "Strategy" character varying(32) NOT NULL DEFAULT 'Wyckoff',
                    "Status" character varying(24) NOT NULL,
                    "Mt5Ticket" character varying(64) NULL,
                    "Mt5OrderId" character varying(64) NULL,
                    "RealizedPnl" numeric(18,2) NULL,
                    "Message" character varying(512) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "ExecutedAt" timestamp with time zone NULL,
                    "ClosedAt" timestamp with time zone NULL,
                    CONSTRAINT "PK_BoletaOrders" PRIMARY KEY ("Id")
                );
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "BoletaSessions"
                    ADD COLUMN IF NOT EXISTS "AllowNeutralEntries" boolean NOT NULL DEFAULT TRUE;
                ALTER TABLE "BoletaSessions"
                    ADD COLUMN IF NOT EXISTS "PreferredDirection" character varying(8) NULL;
                ALTER TABLE "BoletaSessions"
                    ADD COLUMN IF NOT EXISTS "TrailingStopPercent" numeric(8,4) NOT NULL DEFAULT 0;
                ALTER TABLE "BoletaSessions"
                    ADD COLUMN IF NOT EXISTS "TradingEnabled" boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE "BoletaOrders"
                    ADD COLUMN IF NOT EXISTS "PeakFavorablePrice" numeric(18,8) NULL;
                ALTER TABLE "BoletaOrders"
                    ADD COLUMN IF NOT EXISTS "TrailingStopPrice" numeric(18,8) NULL;
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE INDEX IF NOT EXISTS "IX_BoletaSessions_TenantId_Symbol_SessionDate"
                    ON "BoletaSessions" ("TenantId", "Symbol", "SessionDate");
                CREATE INDEX IF NOT EXISTS "IX_BoletaSessions_TenantId_Status"
                    ON "BoletaSessions" ("TenantId", "Status");
                CREATE INDEX IF NOT EXISTS "IX_BoletaOrders_SessionId_Status"
                    ON "BoletaOrders" ("SessionId", "Status");
                CREATE INDEX IF NOT EXISTS "IX_BoletaOrders_TenantId_Symbol_CreatedAt"
                    ON "BoletaOrders" ("TenantId", "Symbol", "CreatedAt");
                """, ct);

            logger.LogInformation("Boletagem schema verified");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Boletagem schema bootstrap failed");
        }
    }
}
