using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bridge.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddGameTimeToBeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "TimeToBeatCompleteSeconds",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "TimeToBeatExtraSeconds",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "TimeToBeatMainSeconds",
                table: "Games",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeToBeatCompleteSeconds",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "TimeToBeatExtraSeconds",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "TimeToBeatMainSeconds",
                table: "Games");
        }
    }
}
