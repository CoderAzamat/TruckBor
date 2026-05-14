using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TruckBor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScrapingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScrapedPosts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceGroupId = table.Column<long>(type: "bigint", nullable: false),
                    SourceGroupTitle = table.Column<string>(type: "text", nullable: true),
                    TelegramMessageId = table.Column<long>(type: "bigint", nullable: false),
                    RawText = table.Column<string>(type: "text", nullable: false),
                    AuthorTelegramId = table.Column<long>(type: "bigint", nullable: true),
                    AuthorName = table.Column<string>(type: "text", nullable: true),
                    MessageDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    PostType = table.Column<int>(type: "integer", nullable: false),
                    FromCity = table.Column<string>(type: "text", nullable: true),
                    ToCity = table.Column<string>(type: "text", nullable: true),
                    CargoType = table.Column<string>(type: "text", nullable: true),
                    Weight = table.Column<string>(type: "text", nullable: true),
                    VehicleType = table.Column<string>(type: "text", nullable: true),
                    Price = table.Column<string>(type: "text", nullable: true),
                    ContactPhone = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Confidence = table.Column<int>(type: "integer", nullable: false),
                    IsRelevant = table.Column<bool>(type: "boolean", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    ContactViews = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrapedPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    SessionString = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsPremium = table.Column<bool>(type: "boolean", nullable: false),
                    IsSpammed = table.Column<bool>(type: "boolean", nullable: false),
                    SpammedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastScrapeAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    JoinedGroupsCount = table.Column<int>(type: "integer", nullable: false),
                    TotalScraped = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScrapedPosts_ExpiresAt",
                table: "ScrapedPosts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScrapedPosts_FromCity",
                table: "ScrapedPosts",
                column: "FromCity");

            migrationBuilder.CreateIndex(
                name: "IX_ScrapedPosts_IsRelevant",
                table: "ScrapedPosts",
                column: "IsRelevant");

            migrationBuilder.CreateIndex(
                name: "IX_ScrapedPosts_MessageDate",
                table: "ScrapedPosts",
                column: "MessageDate");

            migrationBuilder.CreateIndex(
                name: "IX_ScrapedPosts_SourceGroupId_TelegramMessageId",
                table: "ScrapedPosts",
                columns: new[] { "SourceGroupId", "TelegramMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScrapedPosts_ToCity",
                table: "ScrapedPosts",
                column: "ToCity");

            migrationBuilder.CreateIndex(
                name: "IX_SystemAccounts_PhoneNumber",
                table: "SystemAccounts",
                column: "PhoneNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScrapedPosts");

            migrationBuilder.DropTable(
                name: "SystemAccounts");
        }
    }
}
