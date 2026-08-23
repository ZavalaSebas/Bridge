using Bridge.Core.Entities;
using Bridge.Storage.Converters;
using Microsoft.EntityFrameworkCore;

namespace Bridge.Storage;

public class BridgeDbContext(DbContextOptions<BridgeDbContext> options) : DbContext(options)
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Series> Series => Set<Series>();
    public DbSet<AgeRating> AgeRatings => Set<AgeRating>();
    public DbSet<GameFeature> GameFeatures => Set<GameFeature>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Platform> Platforms => Set<Platform>();
    public DbSet<GameSource> GameSources => Set<GameSource>();
    public DbSet<CompletionStatus> CompletionStatuses => Set<CompletionStatus>();
    public DbSet<Emulator> Emulators => Set<Emulator>();
    public DbSet<GameScannerConfig> GameScanners => Set<GameScannerConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Every reference entity is just Id + Name — one line each is enough.
        modelBuilder.Entity<Genre>().HasKey(e => e.Id);
        modelBuilder.Entity<Genre>().HasIndex(e => e.Name).IsUnique();
        modelBuilder.Entity<Category>().HasKey(e => e.Id);
        modelBuilder.Entity<Category>().HasIndex(e => e.Name).IsUnique();
        modelBuilder.Entity<Tag>().HasKey(e => e.Id);
        modelBuilder.Entity<Tag>().HasIndex(e => e.Name).IsUnique();
        modelBuilder.Entity<Series>().HasKey(e => e.Id);
        modelBuilder.Entity<Series>().HasIndex(e => e.Name).IsUnique();
        modelBuilder.Entity<AgeRating>().HasKey(e => e.Id);
        modelBuilder.Entity<AgeRating>().HasIndex(e => e.Name).IsUnique();
        modelBuilder.Entity<GameFeature>().HasKey(e => e.Id);
        modelBuilder.Entity<GameFeature>().HasIndex(e => e.Name).IsUnique();
        modelBuilder.Entity<Company>().HasKey(e => e.Id);
        modelBuilder.Entity<Company>().HasIndex(e => e.Name).IsUnique();
        modelBuilder.Entity<GameSource>().HasKey(e => e.Id);
        modelBuilder.Entity<GameSource>().HasIndex(e => e.Name).IsUnique();

        modelBuilder.Entity<Region>().HasKey(e => e.Id);
        modelBuilder.Entity<Region>().HasIndex(e => e.Name).IsUnique();
        modelBuilder.Entity<Platform>().HasKey(e => e.Id);
        modelBuilder.Entity<Platform>().HasIndex(e => e.Name).IsUnique();
        modelBuilder.Entity<CompletionStatus>().HasKey(e => e.Id);
        modelBuilder.Entity<CompletionStatus>().HasIndex(e => e.Name).IsUnique();

        // Emulator stores its Profiles list as JSON too — same reasoning as Game
        // below: it's a small attached list, not something queried directly in SQL.
        modelBuilder.Entity<Emulator>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Profiles).HasConversion(new JsonValueConverter<List<EmulatorProfile>>());
        });

        modelBuilder.Entity<GameScannerConfig>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ExcludedFiles).HasConversion(new JsonValueConverter<List<string>>());
            e.Property(x => x.ExcludedDirectories).HasConversion(new JsonValueConverter<List<string>>());
            e.Property(x => x.CrcExcludeFileTypes).HasConversion(new JsonValueConverter<List<string>>());
        });

        // Game: scalar fields map to columns automatically. Every List<T>/complex
        // property is stored as a JSON text column via JsonValueConverter — see
        // Converters/JsonValueConverter.cs for why. Reference-entity ids
        // (GenreIds, DeveloperIds, etc.) are stored the same way: Game only ever
        // holds ids; resolving them to Genre/Company/etc. objects is the caller's job.
        modelBuilder.Entity<Game>(e =>
        {
            e.HasKey(g => g.Id);

            e.Ignore(g => g.IsInstalling);
            e.Ignore(g => g.IsUninstalling);
            e.Ignore(g => g.IsLaunching);
            e.Ignore(g => g.IsRunning);
            e.Ignore(g => g.NeedsEmulatorDownload);

            e.Property(g => g.GameActions).HasConversion(new JsonValueConverter<List<GameAction>>());
            e.Property(g => g.Roms).HasConversion(new JsonValueConverter<List<GameRom>>());
            e.Property(g => g.Links).HasConversion(new JsonValueConverter<List<Link>>());
            e.Property(g => g.ReleaseDate).HasConversion(new JsonValueConverter<ReleaseDate?>());
            e.Property(g => g.PlaySessions).HasConversion(new JsonValueConverter<List<GamePlaySession>>());

                e.Property(g => g.GenreIds).HasConversion(new JsonValueConverter<List<Guid>>());
                e.Property(g => g.DescriptionImages).HasConversion(new JsonValueConverter<List<string>>());
                e.Property(g => g.DescriptionBlocks).HasConversion(new JsonValueConverter<List<DescriptionBlock>>());
                e.Property(g => g.Screenshots).HasConversion(new JsonValueConverter<List<string>>());
            e.Property(g => g.DeveloperIds).HasConversion(new JsonValueConverter<List<Guid>>());
            e.Property(g => g.PublisherIds).HasConversion(new JsonValueConverter<List<Guid>>());
            e.Property(g => g.CategoryIds).HasConversion(new JsonValueConverter<List<Guid>>());
            e.Property(g => g.TagIds).HasConversion(new JsonValueConverter<List<Guid>>());
            e.Property(g => g.FeatureIds).HasConversion(new JsonValueConverter<List<Guid>>());
            e.Property(g => g.PlatformIds).HasConversion(new JsonValueConverter<List<Guid>>());
            e.Property(g => g.SeriesIds).HasConversion(new JsonValueConverter<List<Guid>>());
            e.Property(g => g.AgeRatingIds).HasConversion(new JsonValueConverter<List<Guid>>());
            e.Property(g => g.RegionIds).HasConversion(new JsonValueConverter<List<Guid>>());

            // Dedup by store id + source. See ARCHITECTURE.md ADR-6.
            e.HasIndex(g => new { g.ExternalId, g.SourceId }).IsUnique();
        });
    }
}
