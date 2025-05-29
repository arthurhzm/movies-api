using Microsoft.EntityFrameworkCore;
using MoviesAPI.Models;

namespace MoviesAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AuthModel> Auth { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql(
                Environment.GetEnvironmentVariable("DATABASE_URL") ??
                throw new InvalidOperationException("Database connection string is not configured."));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthModel>()
                .HasIndex(u => u.Email)
                .IsUnique();
        base.OnModelCreating(modelBuilder);
    }
}