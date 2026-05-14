using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TruckBor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminWebPanelFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TelegramId = table.Column<long>(type: "bigint", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    IsSuper = table.Column<bool>(type: "boolean", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CanManageUsers = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CanManagePayments = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CanManageTariffs = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageGroups = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageCards = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageChannels = table.Column<bool>(type: "boolean", nullable: false),
                    CanBroadcast = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewStatistics = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CanManageAdmins = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageSettings = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageVirtual = table.Column<bool>(type: "boolean", nullable: false),
                    CanManagePremium = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageVideos = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUsers", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AdminUsers");
        }
    }
}
