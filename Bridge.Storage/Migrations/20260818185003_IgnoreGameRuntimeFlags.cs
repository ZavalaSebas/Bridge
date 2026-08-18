using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bridge.Storage.Migrations
{
    /// <inheritdoc />
    public partial class IgnoreGameRuntimeFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsInstalling",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "IsLaunching",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "IsRunning",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "IsUninstalling",
                table: "Games");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInstalling",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLaunching",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRunning",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsUninstalling",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
