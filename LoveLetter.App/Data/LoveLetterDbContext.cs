using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LoveLetter.App.Data;

public class LoveLetterDbContext(DbContextOptions<LoveLetterDbContext> options) : DbContext(options)
{
    public DbSet<BucketListEntry> BucketListEntries => Set<BucketListEntry>();
    public DbSet<BucketListMedia> BucketListMedia => Set<BucketListMedia>();
    public DbSet<GalleryPhoto> GalleryPhotos => Set<GalleryPhoto>();
    public DbSet<GalleryAlbum> GalleryAlbums => Set<GalleryAlbum>();
    public DbSet<TravelCountry> TravelCountries => Set<TravelCountry>();
    public DbSet<WatchlistMovie> WatchlistMovies => Set<WatchlistMovie>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<TravelCountry>()
            .HasIndex(c => c.CountryCode)
            .IsUnique();

        modelBuilder.Entity<WatchlistMovie>()
            .HasIndex(m => m.ImdbId)
            .IsUnique();
    }
}

public sealed class LoveLetterDbContextFactory : IDesignTimeDbContextFactory<LoveLetterDbContext>
{
    public LoveLetterDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LoveLetterDbContext>();
        optionsBuilder.UseSqlite("Data Source=LoveLetter.DesignTime.db");
        return new LoveLetterDbContext(optionsBuilder.Options);
    }
}
