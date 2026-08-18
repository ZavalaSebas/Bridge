using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bridge.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgeRatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgeRatings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompletionStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompletionStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Emulators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InstallDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    Profiles = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Emulators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameFeatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameFeatures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SortingName = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    DescriptionImages = table.Column<string>(type: "TEXT", nullable: false),
                    DescriptionBlocks = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: false),
                    ReleaseDate = table.Column<string>(type: "TEXT", nullable: true),
                    Icon = table.Column<string>(type: "TEXT", nullable: false),
                    CoverImage = table.Column<string>(type: "TEXT", nullable: false),
                    BackgroundImage = table.Column<string>(type: "TEXT", nullable: false),
                    Screenshots = table.Column<string>(type: "TEXT", nullable: false),
                    IsInstalled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsInstalling = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsUninstalling = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsLaunching = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRunning = table.Column<bool>(type: "INTEGER", nullable: false),
                    OverrideInstallState = table.Column<bool>(type: "INTEGER", nullable: false),
                    InstallDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    InstallSizeBytes = table.Column<ulong>(type: "INTEGER", nullable: true),
                    GameActions = table.Column<string>(type: "TEXT", nullable: false),
                    Roms = table.Column<string>(type: "TEXT", nullable: false),
                    PlaytimeSeconds = table.Column<ulong>(type: "INTEGER", nullable: false),
                    PlayCount = table.Column<ulong>(type: "INTEGER", nullable: false),
                    LastActivity = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Added = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Modified = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PreScript = table.Column<string>(type: "TEXT", nullable: false),
                    PostScript = table.Column<string>(type: "TEXT", nullable: false),
                    GameStartedScript = table.Column<string>(type: "TEXT", nullable: false),
                    UseGlobalPreScript = table.Column<bool>(type: "INTEGER", nullable: false),
                    UseGlobalPostScript = table.Column<bool>(type: "INTEGER", nullable: false),
                    UseGlobalGameStartedScript = table.Column<bool>(type: "INTEGER", nullable: false),
                    Hidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    Favorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserScore = table.Column<int>(type: "INTEGER", nullable: true),
                    CriticScore = table.Column<int>(type: "INTEGER", nullable: true),
                    CommunityScore = table.Column<int>(type: "INTEGER", nullable: true),
                    Links = table.Column<string>(type: "TEXT", nullable: false),
                    GenreIds = table.Column<string>(type: "TEXT", nullable: false),
                    DeveloperIds = table.Column<string>(type: "TEXT", nullable: false),
                    PublisherIds = table.Column<string>(type: "TEXT", nullable: false),
                    CategoryIds = table.Column<string>(type: "TEXT", nullable: false),
                    TagIds = table.Column<string>(type: "TEXT", nullable: false),
                    FeatureIds = table.Column<string>(type: "TEXT", nullable: false),
                    PlatformIds = table.Column<string>(type: "TEXT", nullable: false),
                    SeriesIds = table.Column<string>(type: "TEXT", nullable: false),
                    AgeRatingIds = table.Column<string>(type: "TEXT", nullable: false),
                    RegionIds = table.Column<string>(type: "TEXT", nullable: false),
                    CompletionStatusId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameScanners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EmulatorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EmulatorProfileId = table.Column<string>(type: "TEXT", nullable: false),
                    Directory = table.Column<string>(type: "TEXT", nullable: false),
                    ScanSubfolders = table.Column<bool>(type: "INTEGER", nullable: false),
                    ScanInsideArchives = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExcludeOnlineFiles = table.Column<bool>(type: "INTEGER", nullable: false),
                    UseSimplifiedOnlineFileScan = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImportWithRelativePaths = table.Column<bool>(type: "INTEGER", nullable: false),
                    MergeRelatedFiles = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExcludedFiles = table.Column<string>(type: "TEXT", nullable: false),
                    ExcludedDirectories = table.Column<string>(type: "TEXT", nullable: false),
                    CrcExcludeFileTypes = table.Column<string>(type: "TEXT", nullable: false),
                    OverridePlatformId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayActionMode = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameScanners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Genres",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Platforms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SpecificationId = table.Column<string>(type: "TEXT", nullable: false),
                    Icon = table.Column<string>(type: "TEXT", nullable: false),
                    Cover = table.Column<string>(type: "TEXT", nullable: false),
                    Background = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Platforms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Regions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SpecificationId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Series",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Series", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Games_ExternalId_SourceId",
                table: "Games",
                columns: new[] { "ExternalId", "SourceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgeRatings");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "CompletionStatuses");

            migrationBuilder.DropTable(
                name: "Emulators");

            migrationBuilder.DropTable(
                name: "GameFeatures");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "GameScanners");

            migrationBuilder.DropTable(
                name: "GameSources");

            migrationBuilder.DropTable(
                name: "Genres");

            migrationBuilder.DropTable(
                name: "Platforms");

            migrationBuilder.DropTable(
                name: "Regions");

            migrationBuilder.DropTable(
                name: "Series");

            migrationBuilder.DropTable(
                name: "Tags");
        }
    }
}
