using Microsoft.EntityFrameworkCore;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Api.Services.WhiteLabel;

/// <summary>
/// Idempotente (PostgreSQL): white-label + portfólio quando a migration EF ainda não rodou.
/// Espelha o padrão BoletagemSchemaBootstrap.
/// </summary>
public static class WhiteLabelPortfolioSchemaBootstrap
{
    public static async Task EnsureAsync(NtBotDbContext db, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Tenants"
                    ADD COLUMN IF NOT EXISTS "WhiteLabelEnabled" boolean NOT NULL DEFAULT FALSE;
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "TenantBrandings" (
                    "TenantId" uuid NOT NULL,
                    "AppDisplayName" character varying(120) NOT NULL DEFAULT 'NTBot',
                    "LogoUrl" character varying(512) NULL,
                    "FaviconUrl" character varying(512) NULL,
                    "LoginBackgroundUrl" character varying(512) NULL,
                    "PrimaryColor" character varying(16) NOT NULL DEFAULT '#0ea5e9',
                    "SecondaryColor" character varying(16) NOT NULL DEFAULT '#f0b90b',
                    "BgPrimary" character varying(16) NULL,
                    "BgSecondary" character varying(16) NULL,
                    "TextPrimary" character varying(16) NULL,
                    "LoginHeadline" character varying(200) NULL,
                    "LoginSubtext" character varying(400) NULL,
                    "SupportEmail" character varying(255) NULL,
                    "PublicSlug" character varying(80) NOT NULL,
                    "CustomDomain" character varying(255) NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_TenantBrandings" PRIMARY KEY ("TenantId")
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_TenantBrandings_PublicSlug"
                    ON "TenantBrandings" ("PublicSlug");
                CREATE INDEX IF NOT EXISTS "IX_TenantBrandings_CustomDomain"
                    ON "TenantBrandings" ("CustomDomain");
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "AssetClassNodes" (
                    "Id" uuid NOT NULL,
                    "TenantId" uuid NULL,
                    "Code" character varying(40) NOT NULL,
                    "Name" character varying(120) NOT NULL,
                    "ParentId" uuid NULL,
                    "SortOrder" integer NOT NULL DEFAULT 0,
                    CONSTRAINT "PK_AssetClassNodes" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_AssetClassNodes_TenantId_Code"
                    ON "AssetClassNodes" ("TenantId", "Code");
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "ProductCatalogItems" (
                    "Id" uuid NOT NULL,
                    "TenantId" uuid NULL,
                    "CnpjOrTicker" character varying(40) NOT NULL,
                    "Name" character varying(200) NOT NULL,
                    "AssetClassNodeId" uuid NULL,
                    "ProductFamily" integer NOT NULL DEFAULT 0,
                    "FlagRetailAllowed" boolean NOT NULL DEFAULT TRUE,
                    "CreditRating" character varying(16) NULL,
                    "OffshoreLimitPct" numeric(8,4) NULL,
                    "MetadataJson" text NULL,
                    "IsActive" boolean NOT NULL DEFAULT TRUE,
                    CONSTRAINT "PK_ProductCatalogItems" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_ProductCatalogItems_TenantId_CnpjOrTicker"
                    ON "ProductCatalogItems" ("TenantId", "CnpjOrTicker");
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "ClientInvestorProfiles" (
                    "Id" uuid NOT NULL,
                    "TenantId" uuid NOT NULL,
                    "UserId" uuid NOT NULL,
                    "RiskProfile" integer NOT NULL DEFAULT 1,
                    "Segment" integer NOT NULL DEFAULT 0,
                    "SuitabilityAnswersJson" text NULL,
                    "AssessedAt" timestamp with time zone NULL,
                    "AssessedByUserId" uuid NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_ClientInvestorProfiles" PRIMARY KEY ("Id")
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_ClientInvestorProfiles_TenantId_UserId"
                    ON "ClientInvestorProfiles" ("TenantId", "UserId");
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "GoalPortfolios" (
                    "Id" uuid NOT NULL,
                    "TenantId" uuid NOT NULL,
                    "AdvisorUserId" uuid NOT NULL,
                    "ClientUserId" uuid NOT NULL,
                    "ObjectiveName" character varying(160) NOT NULL,
                    "GoalType" integer NOT NULL DEFAULT 5,
                    "TargetDate" timestamp with time zone NULL,
                    "BaseCurrency" character varying(8) NOT NULL DEFAULT 'BRL',
                    "Status" integer NOT NULL DEFAULT 1,
                    "AnalysisPrompt" character varying(4000) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_GoalPortfolios" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_GoalPortfolios_TenantId_ClientUserId"
                    ON "GoalPortfolios" ("TenantId", "ClientUserId");
                CREATE INDEX IF NOT EXISTS "IX_GoalPortfolios_TenantId_AdvisorUserId"
                    ON "GoalPortfolios" ("TenantId", "AdvisorUserId");
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "GoalPortfolios"
                    ADD COLUMN IF NOT EXISTS "LastAnalysisSummary" character varying(8000) NULL,
                    ADD COLUMN IF NOT EXISTS "LastAnalysisNextBestAction" character varying(1000) NULL,
                    ADD COLUMN IF NOT EXISTS "LastAnalysisAt" timestamp with time zone NULL,
                    ADD COLUMN IF NOT EXISTS "LastAnalysisIsStub" boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE "PortfolioReportJobs"
                    ADD COLUMN IF NOT EXISTS "HtmlContent" text NULL,
                    ADD COLUMN IF NOT EXISTS "PdfContent" bytea NULL,
                    ADD COLUMN IF NOT EXISTS "ReportType" character varying(32) NOT NULL DEFAULT 'RiskAudit',
                    ADD COLUMN IF NOT EXISTS "Version" integer NOT NULL DEFAULT 1;
                ALTER TABLE "ProductCatalogItems"
                    ADD COLUMN IF NOT EXISTS "RiskRating" integer NULL;
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "PortfolioValuationSnapshots" (
                    "Id" uuid NOT NULL,
                    "GoalPortfolioId" uuid NOT NULL,
                    "SnapshotDate" timestamp with time zone NOT NULL,
                    "TotalMarketValue" numeric(18,2) NOT NULL DEFAULT 0,
                    "CostBasis" numeric(18,2) NOT NULL DEFAULT 0,
                    "Notes" character varying(500) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_PortfolioValuationSnapshots" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_PortfolioValuationSnapshots_GoalPortfolioId_SnapshotDate"
                    ON "PortfolioValuationSnapshots" ("GoalPortfolioId", "SnapshotDate");
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "PortfolioCashflows" (
                    "Id" uuid NOT NULL,
                    "GoalPortfolioId" uuid NOT NULL,
                    "CashflowDate" timestamp with time zone NOT NULL,
                    "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                    "Type" integer NOT NULL DEFAULT 0,
                    "Notes" character varying(500) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_PortfolioCashflows" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_PortfolioCashflows_GoalPortfolioId_CashflowDate"
                    ON "PortfolioCashflows" ("GoalPortfolioId", "CashflowDate");
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "PortfolioPositions" (
                    "Id" uuid NOT NULL,
                    "GoalPortfolioId" uuid NOT NULL,
                    "ProductId" uuid NULL,
                    "Symbol" character varying(40) NOT NULL,
                    "CustodianLabel" character varying(80) NULL,
                    "Quantity" numeric(18,8) NOT NULL DEFAULT 0,
                    "AvgPrice" numeric(18,8) NOT NULL DEFAULT 0,
                    "WeightPct" numeric(8,4) NULL,
                    "TargetValue" numeric(18,2) NULL,
                    "MtmValue" numeric(18,2) NULL,
                    "Source" integer NOT NULL DEFAULT 0,
                    "SuitabilityStatus" integer NOT NULL DEFAULT 2,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_PortfolioPositions" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_PortfolioPositions_GoalPortfolioId"
                    ON "PortfolioPositions" ("GoalPortfolioId");
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "PortfolioRecommendations" (
                    "Id" uuid NOT NULL,
                    "GoalPortfolioId" uuid NOT NULL,
                    "ProductId" uuid NOT NULL,
                    "AdvisorUserId" uuid NOT NULL,
                    "EstimatedSharpeImpact" numeric(8,4) NULL,
                    "SimulatedMetricsJson" text NULL,
                    "Notes" character varying(2000) NULL,
                    "Status" integer NOT NULL DEFAULT 0,
                    "ClientRespondedAt" timestamp with time zone NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "ExpiresAt" timestamp with time zone NULL,
                    CONSTRAINT "PK_PortfolioRecommendations" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_PortfolioRecommendations_GoalPortfolioId_Status"
                    ON "PortfolioRecommendations" ("GoalPortfolioId", "Status");
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "PortfolioReportJobs" (
                    "Id" uuid NOT NULL,
                    "TenantId" uuid NOT NULL,
                    "GoalPortfolioId" uuid NOT NULL,
                    "RequestedByUserId" uuid NOT NULL,
                    "Status" character varying(24) NOT NULL DEFAULT 'Pending',
                    "RecipientEmail" character varying(255) NULL,
                    "HtmlContent" text NULL,
                    "ErrorMessage" character varying(1000) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "CompletedAt" timestamp with time zone NULL,
                    CONSTRAINT "PK_PortfolioReportJobs" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_PortfolioReportJobs_TenantId_CreatedAt"
                    ON "PortfolioReportJobs" ("TenantId", "CreatedAt");
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "OpenFinanceConsents" (
                    "Id" uuid NOT NULL,
                    "TenantId" uuid NOT NULL,
                    "ClientUserId" uuid NOT NULL,
                    "Provider" character varying(40) NOT NULL,
                    "ScopeJson" text NULL,
                    "ExpiresAt" timestamp with time zone NULL,
                    "Status" character varying(24) NOT NULL DEFAULT 'Draft',
                    "AuditLogRef" character varying(120) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_OpenFinanceConsents" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_OpenFinanceConsents_TenantId_ClientUserId"
                    ON "OpenFinanceConsents" ("TenantId", "ClientUserId");
                """, ct);

            // Seed partner plan if missing (idempotent insert)
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "Plans" ("Id","Name","Slug","DisplayName","Description","MonthlyPrice","YearlyPrice","Currency",
                    "MaxStrategies","MaxBrokers","MaxTradingAccounts","MaxActivePositions","FeaturesJson","IsActive","SortOrder","CreatedAt")
                SELECT 'dddddddd-0000-0000-0000-000000000001'::uuid, 'Partner', 'partner', 'Partner White-label',
                    'White-label completo + módulo de assessoria / portfólio', 399, 3990, 'USD',
                    50, 10, 50, 50,
                    '{"branding.enabled":true,"branding.custom_login":true,"portfolio.module":true,"portfolio.max_clients":200}',
                    TRUE, 3, TIMESTAMPTZ '2026-01-01 00:00:00+00'
                WHERE NOT EXISTS (SELECT 1 FROM "Plans" WHERE "Slug" = 'partner');
                """, ct);

            await SeedCatalogIfEmptyAsync(db, ct);

            logger.LogInformation("White-label / portfolio schema verified");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "White-label / portfolio schema bootstrap failed");
        }
    }

    private static async Task SeedCatalogIfEmptyAsync(NtBotDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "AssetClassNodes" ("Id","TenantId","Code","Name","ParentId","SortOrder")
            SELECT 'a1000001-0000-0000-0000-000000000001'::uuid, NULL, 'RF', 'Renda Fixa', NULL, 1
            WHERE NOT EXISTS (SELECT 1 FROM "AssetClassNodes" WHERE "Code" = 'RF' AND "TenantId" IS NULL);
            INSERT INTO "AssetClassNodes" ("Id","TenantId","Code","Name","ParentId","SortOrder")
            SELECT 'a1000001-0000-0000-0000-000000000002'::uuid, NULL, 'RV', 'Renda Variável', NULL, 2
            WHERE NOT EXISTS (SELECT 1 FROM "AssetClassNodes" WHERE "Code" = 'RV' AND "TenantId" IS NULL);
            INSERT INTO "AssetClassNodes" ("Id","TenantId","Code","Name","ParentId","SortOrder")
            SELECT 'a1000001-0000-0000-0000-000000000003'::uuid, NULL, 'FUNDOS', 'Fundos Estruturados', NULL, 3
            WHERE NOT EXISTS (SELECT 1 FROM "AssetClassNodes" WHERE "Code" = 'FUNDOS' AND "TenantId" IS NULL);
            INSERT INTO "AssetClassNodes" ("Id","TenantId","Code","Name","ParentId","SortOrder")
            SELECT 'a1000001-0000-0000-0000-000000000004'::uuid, NULL, 'CAIXA', 'Caixa', NULL, 4
            WHERE NOT EXISTS (SELECT 1 FROM "AssetClassNodes" WHERE "Code" = 'CAIXA' AND "TenantId" IS NULL);
            INSERT INTO "AssetClassNodes" ("Id","TenantId","Code","Name","ParentId","SortOrder")
            SELECT 'a1000001-0000-0000-0000-000000000011'::uuid, NULL, 'FIF', 'FIF — Fundos Financeiros',
                'a1000001-0000-0000-0000-000000000003'::uuid, 1
            WHERE NOT EXISTS (SELECT 1 FROM "AssetClassNodes" WHERE "Code" = 'FIF' AND "TenantId" IS NULL);
            INSERT INTO "AssetClassNodes" ("Id","TenantId","Code","Name","ParentId","SortOrder")
            SELECT 'a1000001-0000-0000-0000-000000000012'::uuid, NULL, 'FII', 'FII — Imobiliário',
                'a1000001-0000-0000-0000-000000000003'::uuid, 2
            WHERE NOT EXISTS (SELECT 1 FROM "AssetClassNodes" WHERE "Code" = 'FII' AND "TenantId" IS NULL);
            INSERT INTO "AssetClassNodes" ("Id","TenantId","Code","Name","ParentId","SortOrder")
            SELECT 'a1000001-0000-0000-0000-000000000013'::uuid, NULL, 'FIDC', 'FIDC — Direitos Creditórios',
                'a1000001-0000-0000-0000-000000000003'::uuid, 3
            WHERE NOT EXISTS (SELECT 1 FROM "AssetClassNodes" WHERE "Code" = 'FIDC' AND "TenantId" IS NULL);
            INSERT INTO "AssetClassNodes" ("Id","TenantId","Code","Name","ParentId","SortOrder")
            SELECT 'a1000001-0000-0000-0000-000000000014'::uuid, NULL, 'FIAGRO', 'FIAGRO — Agro',
                'a1000001-0000-0000-0000-000000000003'::uuid, 4
            WHERE NOT EXISTS (SELECT 1 FROM "AssetClassNodes" WHERE "Code" = 'FIAGRO' AND "TenantId" IS NULL);
            INSERT INTO "AssetClassNodes" ("Id","TenantId","Code","Name","ParentId","SortOrder")
            SELECT 'a1000001-0000-0000-0000-000000000015'::uuid, NULL, 'FIP', 'FIP — Participações',
                'a1000001-0000-0000-0000-000000000003'::uuid, 5
            WHERE NOT EXISTS (SELECT 1 FROM "AssetClassNodes" WHERE "Code" = 'FIP' AND "TenantId" IS NULL);
            """, ct);

        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "ProductCatalogItems"
                ("Id","TenantId","CnpjOrTicker","Name","AssetClassNodeId","ProductFamily","FlagRetailAllowed","CreditRating","RiskRating","OffshoreLimitPct","MetadataJson","IsActive")
            SELECT 'b1000001-0000-0000-0000-000000000001'::uuid, NULL, '00.000.000/0001-01', 'FIF Liquidez Conservador',
                'a1000001-0000-0000-0000-000000000011'::uuid, 0, TRUE, 'AAA', 1, NULL, NULL, TRUE
            WHERE NOT EXISTS (SELECT 1 FROM "ProductCatalogItems" WHERE "Id" = 'b1000001-0000-0000-0000-000000000001'::uuid);
            INSERT INTO "ProductCatalogItems"
                ("Id","TenantId","CnpjOrTicker","Name","AssetClassNodeId","ProductFamily","FlagRetailAllowed","CreditRating","RiskRating","OffshoreLimitPct","MetadataJson","IsActive")
            SELECT 'b1000001-0000-0000-0000-000000000002'::uuid, NULL, 'HGLG11', 'FII Logística Exemplo',
                'a1000001-0000-0000-0000-000000000012'::uuid, 1, TRUE, NULL, 3, NULL, NULL, TRUE
            WHERE NOT EXISTS (SELECT 1 FROM "ProductCatalogItems" WHERE "Id" = 'b1000001-0000-0000-0000-000000000002'::uuid);
            INSERT INTO "ProductCatalogItems"
                ("Id","TenantId","CnpjOrTicker","Name","AssetClassNodeId","ProductFamily","FlagRetailAllowed","CreditRating","RiskRating","OffshoreLimitPct","MetadataJson","IsActive")
            SELECT 'b1000001-0000-0000-0000-000000000003'::uuid, NULL, '00.000.000/0001-99', 'FIDC Sênior AAA',
                'a1000001-0000-0000-0000-000000000013'::uuid, 2, TRUE, 'AAA', 2, NULL, '{"tranche":"senior","max_redeem_days":90}', TRUE
            WHERE NOT EXISTS (SELECT 1 FROM "ProductCatalogItems" WHERE "Id" = 'b1000001-0000-0000-0000-000000000003'::uuid);
            INSERT INTO "ProductCatalogItems"
                ("Id","TenantId","CnpjOrTicker","Name","AssetClassNodeId","ProductFamily","FlagRetailAllowed","CreditRating","RiskRating","OffshoreLimitPct","MetadataJson","IsActive")
            SELECT 'b1000001-0000-0000-0000-000000000004'::uuid, NULL, '00.000.000/0001-88', 'FIDC Subordinado (Qualificado)',
                'a1000001-0000-0000-0000-000000000013'::uuid, 2, FALSE, 'BB', 5, NULL, '{"tranche":"subordinated"}', TRUE
            WHERE NOT EXISTS (SELECT 1 FROM "ProductCatalogItems" WHERE "Id" = 'b1000001-0000-0000-0000-000000000004'::uuid);

            INSERT INTO "ProductCatalogItems"
                ("Id","TenantId","CnpjOrTicker","Name","AssetClassNodeId","ProductFamily","FlagRetailAllowed","CreditRating","RiskRating","OffshoreLimitPct","MetadataJson","IsActive")
            SELECT 'b1000001-0000-0000-0000-000000000005'::uuid, NULL, '00.000.000/0001-02', 'FIF DI Referenciado',
                'a1000001-0000-0000-0000-000000000011'::uuid, 0, TRUE, 'AAA', 1, NULL, NULL, TRUE
            WHERE NOT EXISTS (SELECT 1 FROM "ProductCatalogItems" WHERE "Id" = 'b1000001-0000-0000-0000-000000000005'::uuid);
            INSERT INTO "ProductCatalogItems"
                ("Id","TenantId","CnpjOrTicker","Name","AssetClassNodeId","ProductFamily","FlagRetailAllowed","CreditRating","RiskRating","OffshoreLimitPct","MetadataJson","IsActive")
            SELECT 'b1000001-0000-0000-0000-000000000006'::uuid, NULL, '00.000.000/0001-03', 'FIF Multimercado Moderado',
                'a1000001-0000-0000-0000-000000000011'::uuid, 0, TRUE, 'AA', 3, NULL, NULL, TRUE
            WHERE NOT EXISTS (SELECT 1 FROM "ProductCatalogItems" WHERE "Id" = 'b1000001-0000-0000-0000-000000000006'::uuid);
            INSERT INTO "ProductCatalogItems"
                ("Id","TenantId","CnpjOrTicker","Name","AssetClassNodeId","ProductFamily","FlagRetailAllowed","CreditRating","RiskRating","OffshoreLimitPct","MetadataJson","IsActive")
            SELECT 'b1000001-0000-0000-0000-000000000007'::uuid, NULL, 'XPML11', 'FII Shopping Exemplo',
                'a1000001-0000-0000-0000-000000000012'::uuid, 1, TRUE, NULL, 3, NULL, NULL, TRUE
            WHERE NOT EXISTS (SELECT 1 FROM "ProductCatalogItems" WHERE "Id" = 'b1000001-0000-0000-0000-000000000007'::uuid);
            INSERT INTO "ProductCatalogItems"
                ("Id","TenantId","CnpjOrTicker","Name","AssetClassNodeId","ProductFamily","FlagRetailAllowed","CreditRating","RiskRating","OffshoreLimitPct","MetadataJson","IsActive")
            SELECT 'b1000001-0000-0000-0000-000000000008'::uuid, NULL, 'KNRI11', 'FII Lajes Corporativas',
                'a1000001-0000-0000-0000-000000000012'::uuid, 1, TRUE, NULL, 3, NULL, NULL, TRUE
            WHERE NOT EXISTS (SELECT 1 FROM "ProductCatalogItems" WHERE "Id" = 'b1000001-0000-0000-0000-000000000008'::uuid);
            INSERT INTO "ProductCatalogItems"
                ("Id","TenantId","CnpjOrTicker","Name","AssetClassNodeId","ProductFamily","FlagRetailAllowed","CreditRating","RiskRating","OffshoreLimitPct","MetadataJson","IsActive")
            SELECT 'b1000001-0000-0000-0000-000000000009'::uuid, NULL, '00.000.000/0001-77', 'FIDC Sênior AA Varejo',
                'a1000001-0000-0000-0000-000000000013'::uuid, 2, TRUE, 'AA', 2, NULL, '{"tranche":"senior","max_redeem_days":60}', TRUE
            WHERE NOT EXISTS (SELECT 1 FROM "ProductCatalogItems" WHERE "Id" = 'b1000001-0000-0000-0000-000000000009'::uuid);
            INSERT INTO "ProductCatalogItems"
                ("Id","TenantId","CnpjOrTicker","Name","AssetClassNodeId","ProductFamily","FlagRetailAllowed","CreditRating","RiskRating","OffshoreLimitPct","MetadataJson","IsActive")
            SELECT 'b1000001-0000-0000-0000-00000000000a'::uuid, NULL, '00.000.000/0001-66', 'FIDC Mezanino (Qualificado)',
                'a1000001-0000-0000-0000-000000000013'::uuid, 2, FALSE, 'BBB', 4, NULL, '{"tranche":"mezzanine"}', TRUE
            WHERE NOT EXISTS (SELECT 1 FROM "ProductCatalogItems" WHERE "Id" = 'b1000001-0000-0000-0000-00000000000a'::uuid);

            INSERT INTO "ProductCatalogItems"
                ("Id","TenantId","CnpjOrTicker","Name","AssetClassNodeId","ProductFamily","FlagRetailAllowed","CreditRating","RiskRating","OffshoreLimitPct","MetadataJson","IsActive")
            SELECT 'b1000001-0000-0000-0000-00000000000b'::uuid, NULL, '00.000.000/0001-55', 'FIAGRO Crédito Exemplo',
                'a1000001-0000-0000-0000-000000000014'::uuid, 3, TRUE, 'A', 3, NULL, '{"cvm175":"fiagro"}', TRUE
            WHERE NOT EXISTS (SELECT 1 FROM "ProductCatalogItems" WHERE "Id" = 'b1000001-0000-0000-0000-00000000000b'::uuid);
            INSERT INTO "ProductCatalogItems"
                ("Id","TenantId","CnpjOrTicker","Name","AssetClassNodeId","ProductFamily","FlagRetailAllowed","CreditRating","RiskRating","OffshoreLimitPct","MetadataJson","IsActive")
            SELECT 'b1000001-0000-0000-0000-00000000000c'::uuid, NULL, '00.000.000/0001-44', 'FIAGRO Imobiliário Rural',
                'a1000001-0000-0000-0000-000000000014'::uuid, 3, TRUE, NULL, 3, NULL, '{"cvm175":"fiagro"}', TRUE
            WHERE NOT EXISTS (SELECT 1 FROM "ProductCatalogItems" WHERE "Id" = 'b1000001-0000-0000-0000-00000000000c'::uuid);
            INSERT INTO "ProductCatalogItems"
                ("Id","TenantId","CnpjOrTicker","Name","AssetClassNodeId","ProductFamily","FlagRetailAllowed","CreditRating","RiskRating","OffshoreLimitPct","MetadataJson","IsActive")
            SELECT 'b1000001-0000-0000-0000-00000000000d'::uuid, NULL, '00.000.000/0001-33', 'FIP Private Equity (Qualificado)',
                'a1000001-0000-0000-0000-000000000015'::uuid, 4, FALSE, NULL, 5, 20, '{"cvm175":"fip","segment":"qualified"}', TRUE
            WHERE NOT EXISTS (SELECT 1 FROM "ProductCatalogItems" WHERE "Id" = 'b1000001-0000-0000-0000-00000000000d'::uuid);
            INSERT INTO "ProductCatalogItems"
                ("Id","TenantId","CnpjOrTicker","Name","AssetClassNodeId","ProductFamily","FlagRetailAllowed","CreditRating","RiskRating","OffshoreLimitPct","MetadataJson","IsActive")
            SELECT 'b1000001-0000-0000-0000-00000000000e'::uuid, NULL, '00.000.000/0001-22', 'FIF Offshore Global (limite 20%)',
                'a1000001-0000-0000-0000-000000000011'::uuid, 0, FALSE, 'AA', 4, 20, '{"offshore":true}', TRUE
            WHERE NOT EXISTS (SELECT 1 FROM "ProductCatalogItems" WHERE "Id" = 'b1000001-0000-0000-0000-00000000000e'::uuid);

            UPDATE "ProductCatalogItems" SET "RiskRating" = 1 WHERE "Id" = 'b1000001-0000-0000-0000-000000000001'::uuid AND "RiskRating" IS NULL;
            UPDATE "ProductCatalogItems" SET "RiskRating" = 3 WHERE "Id" = 'b1000001-0000-0000-0000-000000000002'::uuid AND "RiskRating" IS NULL;
            UPDATE "ProductCatalogItems" SET "RiskRating" = 2 WHERE "Id" = 'b1000001-0000-0000-0000-000000000003'::uuid AND "RiskRating" IS NULL;
            UPDATE "ProductCatalogItems" SET "RiskRating" = 5 WHERE "Id" = 'b1000001-0000-0000-0000-000000000004'::uuid AND "RiskRating" IS NULL;
            """, ct);
    }
}
