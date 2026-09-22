using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NtBot.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddWhiteLabelPortfolio : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "WhiteLabelEnabled",
            table: "Tenants",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "TenantBrandings",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                AppDisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                LogoUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                FaviconUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                LoginBackgroundUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                PrimaryColor = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                SecondaryColor = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                BgPrimary = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                BgSecondary = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                TextPrimary = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                LoginHeadline = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                LoginSubtext = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                SupportEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                PublicSlug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                CustomDomain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TenantBrandings", x => x.TenantId);
                table.ForeignKey(
                    name: "FK_TenantBrandings_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AssetClassNodes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                SortOrder = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AssetClassNodes", x => x.Id);
                table.ForeignKey(
                    name: "FK_AssetClassNodes_AssetClassNodes_ParentId",
                    column: x => x.ParentId,
                    principalTable: "AssetClassNodes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ClientInvestorProfiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RiskProfile = table.Column<int>(type: "integer", nullable: false),
                Segment = table.Column<int>(type: "integer", nullable: false),
                SuitabilityAnswersJson = table.Column<string>(type: "text", nullable: true),
                AssessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                AssessedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ClientInvestorProfiles", x => x.Id));

        migrationBuilder.CreateTable(
            name: "GoalPortfolios",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                AdvisorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                ClientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                ObjectiveName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                GoalType = table.Column<int>(type: "integer", nullable: false),
                TargetDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                BaseCurrency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                AnalysisPrompt = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_GoalPortfolios", x => x.Id));

        migrationBuilder.CreateTable(
            name: "OpenFinanceConsents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ClientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                ScopeJson = table.Column<string>(type: "text", nullable: true),
                ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                AuditLogRef = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_OpenFinanceConsents", x => x.Id));

        migrationBuilder.CreateTable(
            name: "PortfolioReportJobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                GoalPortfolioId = table.Column<Guid>(type: "uuid", nullable: false),
                RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                RecipientEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_PortfolioReportJobs", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ProductCatalogItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                CnpjOrTicker = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                AssetClassNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                ProductFamily = table.Column<int>(type: "integer", nullable: false),
                FlagRetailAllowed = table.Column<bool>(type: "boolean", nullable: false),
                CreditRating = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                OffshoreLimitPct = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                MetadataJson = table.Column<string>(type: "text", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductCatalogItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProductCatalogItems_AssetClassNodes_AssetClassNodeId",
                    column: x => x.AssetClassNodeId,
                    principalTable: "AssetClassNodes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "PortfolioPositions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                GoalPortfolioId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                Symbol = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CustodianLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                Quantity = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                AvgPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                WeightPct = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                TargetValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                MtmValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                Source = table.Column<int>(type: "integer", nullable: false),
                SuitabilityStatus = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PortfolioPositions", x => x.Id);
                table.ForeignKey(
                    name: "FK_PortfolioPositions_GoalPortfolios_GoalPortfolioId",
                    column: x => x.GoalPortfolioId,
                    principalTable: "GoalPortfolios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PortfolioPositions_ProductCatalogItems_ProductId",
                    column: x => x.ProductId,
                    principalTable: "ProductCatalogItems",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "PortfolioRecommendations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                GoalPortfolioId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                AdvisorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                EstimatedSharpeImpact = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                SimulatedMetricsJson = table.Column<string>(type: "text", nullable: true),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                ClientRespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PortfolioRecommendations", x => x.Id);
                table.ForeignKey(
                    name: "FK_PortfolioRecommendations_GoalPortfolios_GoalPortfolioId",
                    column: x => x.GoalPortfolioId,
                    principalTable: "GoalPortfolios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PortfolioRecommendations_ProductCatalogItems_ProductId",
                    column: x => x.ProductId,
                    principalTable: "ProductCatalogItems",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_TenantBrandings_PublicSlug", table: "TenantBrandings", column: "PublicSlug", unique: true);
        migrationBuilder.CreateIndex(name: "IX_TenantBrandings_CustomDomain", table: "TenantBrandings", column: "CustomDomain");
        migrationBuilder.CreateIndex(name: "IX_AssetClassNodes_ParentId", table: "AssetClassNodes", column: "ParentId");
        migrationBuilder.CreateIndex(name: "IX_AssetClassNodes_TenantId_Code", table: "AssetClassNodes", columns: new[] { "TenantId", "Code" });
        migrationBuilder.CreateIndex(name: "IX_ClientInvestorProfiles_TenantId_UserId", table: "ClientInvestorProfiles", columns: new[] { "TenantId", "UserId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_GoalPortfolios_TenantId_ClientUserId", table: "GoalPortfolios", columns: new[] { "TenantId", "ClientUserId" });
        migrationBuilder.CreateIndex(name: "IX_GoalPortfolios_TenantId_AdvisorUserId", table: "GoalPortfolios", columns: new[] { "TenantId", "AdvisorUserId" });
        migrationBuilder.CreateIndex(name: "IX_OpenFinanceConsents_TenantId_ClientUserId", table: "OpenFinanceConsents", columns: new[] { "TenantId", "ClientUserId" });
        migrationBuilder.CreateIndex(name: "IX_PortfolioReportJobs_TenantId_CreatedAt", table: "PortfolioReportJobs", columns: new[] { "TenantId", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_ProductCatalogItems_AssetClassNodeId", table: "ProductCatalogItems", column: "AssetClassNodeId");
        migrationBuilder.CreateIndex(name: "IX_ProductCatalogItems_TenantId_CnpjOrTicker", table: "ProductCatalogItems", columns: new[] { "TenantId", "CnpjOrTicker" });
        migrationBuilder.CreateIndex(name: "IX_PortfolioPositions_GoalPortfolioId", table: "PortfolioPositions", column: "GoalPortfolioId");
        migrationBuilder.CreateIndex(name: "IX_PortfolioPositions_ProductId", table: "PortfolioPositions", column: "ProductId");
        migrationBuilder.CreateIndex(name: "IX_PortfolioRecommendations_GoalPortfolioId_Status", table: "PortfolioRecommendations", columns: new[] { "GoalPortfolioId", "Status" });
        migrationBuilder.CreateIndex(name: "IX_PortfolioRecommendations_ProductId", table: "PortfolioRecommendations", column: "ProductId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PortfolioRecommendations");
        migrationBuilder.DropTable(name: "PortfolioPositions");
        migrationBuilder.DropTable(name: "PortfolioReportJobs");
        migrationBuilder.DropTable(name: "OpenFinanceConsents");
        migrationBuilder.DropTable(name: "ClientInvestorProfiles");
        migrationBuilder.DropTable(name: "ProductCatalogItems");
        migrationBuilder.DropTable(name: "GoalPortfolios");
        migrationBuilder.DropTable(name: "AssetClassNodes");
        migrationBuilder.DropTable(name: "TenantBrandings");
        migrationBuilder.DropColumn(name: "WhiteLabelEnabled", table: "Tenants");
    }
}
