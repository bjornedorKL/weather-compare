using Microsoft.EntityFrameworkCore;

namespace WeatherCompare.Api.Storage;

public class WeatherDbContext(DbContextOptions<WeatherDbContext> options) : DbContext(options)
{
    public DbSet<ForecastSnapshot> ForecastSnapshots => Set<ForecastSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
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

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        GuardAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// A Forecast Snapshot is what a Provider told us at a moment; changing one destroys
    /// the record that they ever said it. A refresh appends, it never updates (ADR-0001).
    /// </summary>
    private void GuardAppendOnly()
    {
        var offending = ChangeTracker
            .Entries()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted)
            .Select(e => $"{e.Entity.GetType().Name} ({e.State})")
            .ToList();

        if (offending.Count > 0)
        {
            throw new InvalidOperationException(
                "The Forecast Snapshot store is append-only; refused: " + string.Join(", ", offending));
        }
    }
}
