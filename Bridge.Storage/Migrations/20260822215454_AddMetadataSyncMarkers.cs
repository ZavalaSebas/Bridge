using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bridge.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddMetadataSyncMarkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LinksSyncedAt",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MetadataSyncedAt",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TimeToBeatSyncedAt",
                table: "Games",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LinksSyncedAt",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "MetadataSyncedAt",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "TimeToBeatSyncedAt",
                table: "Games");
        }
    }
}
