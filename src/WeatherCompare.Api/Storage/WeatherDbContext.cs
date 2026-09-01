using Microsoft.EntityFrameworkCore;
using WeatherCompare.Api.Locations;

namespace WeatherCompare.Api.Storage;

public class WeatherDbContext(DbContextOptions<WeatherDbContext> options) : DbContext(options)
{
    public DbSet<ForecastSnapshot> ForecastSnapshots => Set<ForecastSnapshot>();

    public DbSet<Location> Locations => Set<Location>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        MapForecastSnapshots(modelBuilder);
        MapLocations(modelBuilder);
    }

    private static void MapForecastSnapshots(ModelBuilder modelBuilder)
    {
        var snapshot = modelBuilder.Entity<ForecastSnapshot>();

        snapshot.ToTable("forecast_snapshots");
        snapshot.HasKey(s => s.Id);
        snapshot.Property(s => s.Provider).HasMaxLength(100);

        // Coordinate precision: four decimals is what Locationforecast accepts.
        snapshot.Property(s => s.Latitude).HasPrecision(8, 4);
        snapshot.Property(s => s.Longitude).HasPrecision(8, 4);

        // The only read the page performs: newest Snapshot for a (Provider, Location) pair.
        snapshot
            .HasIndex(s => new { s.Provider, s.Latitude, s.Longitude, s.IssuedAt })
            .HasDatabaseName("ix_forecast_snapshots_provider_location_issued_at_desc")
            .IsDescending(false, false, false, true);
    }

    private static void MapLocations(ModelBuilder modelBuilder)
    {
        var location = modelBuilder.Entity<Location>();

        location.ToTable("locations");
        location.HasKey(l => l.Id);
        location.Property(l => l.Name).HasMaxLength(100);

        // A Location is its coordinate at the precision the Provider accepts, so the store
        // refuses to hold the same coordinate twice however the two rows are named.
        location.Property(l => l.Latitude).HasPrecision(8, 4);
        location.Property(l => l.Longitude).HasPrecision(8, 4);
        location
            .HasIndex(l => new { l.Latitude, l.Longitude })
            .HasDatabaseName("ux_locations_coordinate")
            .IsUnique();

        // The tracked subset is the Catalogue, and it is all the poller and the page ever read.
        // A Location arrives tracked; untracking is the deliberate act (ADR-0003). The column
        // default says so for anyone writing SQL by hand, but the value we hold is always sent:
        // without ValueGeneratedNever, inserting an untracked Location would silently store
        // the default and track it.
        location.Property(l => l.Tracked).HasDefaultValue(true).ValueGeneratedNever();

        // Identity, not state: derived from the two columns above.
        location.Ignore(l => l.Coordinate);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardSnapshotsAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        GuardSnapshotsAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// A Forecast Snapshot is what a Provider told us at a moment; changing one destroys the
    /// record that they ever said it. A refresh appends, it never updates (ADR-0001).
    /// <para>
    /// Only Snapshots are append-only. Locations are not: untracking one sets a flag on its row,
    /// and the row survives so the Location can be tracked again (ADR-0003). The guard therefore
    /// names the entity it protects rather than refusing every update this store could make.
    /// </para>
    /// </summary>
    private void GuardSnapshotsAppendOnly()
    {
        var offending = ChangeTracker
            .Entries<ForecastSnapshot>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted)
            .Select(e => $"Forecast Snapshot {e.Entity.Id} ({e.State})")
            .ToList();

        if (offending.Count > 0)
        {
            throw new InvalidOperationException(
                "The Forecast Snapshot store is append-only; refused: " + string.Join(", ", offending));
        }
    }
}
