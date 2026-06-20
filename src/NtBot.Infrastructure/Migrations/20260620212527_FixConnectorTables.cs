using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NtBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixConnectorTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConnectorKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RotatedFromKeyId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectorKeys_ConnectorKeys_RotatedFromKeyId",
                        column: x => x.RotatedFromKeyId,
                        principalTable: "ConnectorKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConnectorKeys_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConnectorVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReleaseNotes = table.Column<string>(type: "text", nullable: false),
                    Sha256Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    MinPlan = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConnectorSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectorKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ConnectorVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MachineName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OsVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastHeartbeatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisconnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectorSessions_ConnectorKeys_ConnectorKeyId",
                        column: x => x.ConnectorKeyId,
                        principalTable: "ConnectorKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConnectorSessions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConnectorDownloads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectorKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectorVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DownloadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorDownloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectorDownloads_ConnectorKeys_ConnectorKeyId",
                        column: x => x.ConnectorKeyId,
                        principalTable: "ConnectorKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConnectorDownloads_ConnectorVersions_ConnectorVersionId",
                        column: x => x.ConnectorVersionId,
                        principalTable: "ConnectorVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConnectorDownloads_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConnectorLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConnectorKeyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConnectorSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectorLogs_ConnectorKeys_ConnectorKeyId",
                        column: x => x.ConnectorKeyId,
                        principalTable: "ConnectorKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConnectorLogs_ConnectorSessions_ConnectorSessionId",
                        column: x => x.ConnectorSessionId,
                        principalTable: "ConnectorSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConnectorLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "ConnectorVersions",
                columns: new[] { "Id", "Channel", "CreatedAt", "FileName", "FileSizeBytes", "IsPublished", "MinPlan", "PublishedAt", "ReleaseNotes", "Sha256Hash", "Version" },
                values: new object[] { new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), "stable", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "NtBot.Connector.Windows-1.0.0.zip", 0L, true, 0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Versão inicial do NtBot Connector Windows.", "0000000000000000000000000000000000000000000000000000000000000000", "1.0.0" });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorDownloads_ConnectorKeyId",
                table: "ConnectorDownloads",
                column: "ConnectorKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorDownloads_ConnectorVersionId",
                table: "ConnectorDownloads",
                column: "ConnectorVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorDownloads_TenantId_DownloadedAt",
                table: "ConnectorDownloads",
                columns: new[] { "TenantId", "DownloadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorKeys_KeyPrefix",
                table: "ConnectorKeys",
                column: "KeyPrefix");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorKeys_RotatedFromKeyId",
                table: "ConnectorKeys",
                column: "RotatedFromKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorKeys_TenantId_IsActive",
                table: "ConnectorKeys",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorLogs_ConnectorKeyId",
                table: "ConnectorLogs",
                column: "ConnectorKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorLogs_ConnectorSessionId",
                table: "ConnectorLogs",
                column: "ConnectorSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorLogs_TenantId_CreatedAt",
                table: "ConnectorLogs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorSessions_ConnectorKeyId",
                table: "ConnectorSessions",
                column: "ConnectorKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorSessions_LastHeartbeatAt",
                table: "ConnectorSessions",
                column: "LastHeartbeatAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorSessions_TenantId_Status",
                table: "ConnectorSessions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorVersions_IsPublished",
                table: "ConnectorVersions",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorVersions_Version_Channel",
                table: "ConnectorVersions",
                columns: new[] { "Version", "Channel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConnectorDownloads");

            migrationBuilder.DropTable(
                name: "ConnectorLogs");

            migrationBuilder.DropTable(
                name: "ConnectorVersions");

            migrationBuilder.DropTable(
                name: "ConnectorSessions");

            migrationBuilder.DropTable(
                name: "ConnectorKeys");
        }
    }
}
