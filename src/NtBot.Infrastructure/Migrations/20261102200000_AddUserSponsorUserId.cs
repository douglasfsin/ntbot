using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NtBot.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddUserSponsorUserId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "SponsorUserId",
            table: "Users",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_SponsorUserId",
            table: "Users",
            column: "SponsorUserId");

        migrationBuilder.AddForeignKey(
            name: "FK_Users_Users_SponsorUserId",
            table: "Users",
            column: "SponsorUserId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Users_Users_SponsorUserId",
            table: "Users");

        migrationBuilder.DropIndex(
            name: "IX_Users_SponsorUserId",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "SponsorUserId",
            table: "Users");
    }
}
