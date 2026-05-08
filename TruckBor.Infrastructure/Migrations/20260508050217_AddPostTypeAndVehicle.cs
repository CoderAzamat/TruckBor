using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckBor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostTypeAndVehicle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TransportType",
                table: "Posts",
                newName: "VehicleType");

            migrationBuilder.AddColumn<int>(
                name: "ContactViews",
                table: "Posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "Posts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PostType",
                table: "Posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ViewCount",
                table: "Posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactViews",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "PostType",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "ViewCount",
                table: "Posts");

            migrationBuilder.RenameColumn(
                name: "VehicleType",
                table: "Posts",
                newName: "TransportType");
        }
    }
}
