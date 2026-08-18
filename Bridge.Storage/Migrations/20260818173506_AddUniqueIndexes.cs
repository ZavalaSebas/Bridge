using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bridge.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Games_ExternalId_SourceId",
                table: "Games");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Series_Name",
                table: "Series",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Regions_Name",
                table: "Regions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Platforms_Name",
                table: "Platforms",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Genres_Name",
                table: "Genres",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameSources_Name",
                table: "GameSources",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_ExternalId_SourceId",
                table: "Games",
                columns: new[] { "ExternalId", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameFeatures_Name",
                table: "GameFeatures",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompletionStatuses_Name",
                table: "CompletionStatuses",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Name",
                table: "Companies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgeRatings_Name",
                table: "AgeRatings",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_Name",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Series_Name",
                table: "Series");

            migrationBuilder.DropIndex(
                name: "IX_Regions_Name",
                table: "Regions");

            migrationBuilder.DropIndex(
                name: "IX_Platforms_Name",
                table: "Platforms");

            migrationBuilder.DropIndex(
                name: "IX_Genres_Name",
                table: "Genres");

            migrationBuilder.DropIndex(
                name: "IX_GameSources_Name",
                table: "GameSources");

            migrationBuilder.DropIndex(
                name: "IX_Games_ExternalId_SourceId",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_GameFeatures_Name",
                table: "GameFeatures");

            migrationBuilder.DropIndex(
                name: "IX_CompletionStatuses_Name",
                table: "CompletionStatuses");

            migrationBuilder.DropIndex(
                name: "IX_Companies_Name",
                table: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_AgeRatings_Name",
                table: "AgeRatings");

            migrationBuilder.CreateIndex(
                name: "IX_Games_ExternalId_SourceId",
                table: "Games",
                columns: new[] { "ExternalId", "SourceId" });
        }
    }
}
