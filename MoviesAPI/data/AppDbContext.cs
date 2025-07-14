using Microsoft.EntityFrameworkCore;
using MoviesAPI.Models;

namespace MoviesAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AuthModel> Auth { get; set; } = null!;
    public DbSet<ActorsModel> Actors { get; set; } = null!;
    public DbSet<DirectorsModel> Directors { get; set; } = null!;
    public DbSet<GenresModel> Genres { get; set; } = null!;
    public DbSet<UserPreferencesModel> UserPreferences { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthModel>()
                .HasIndex(u => u.Email)
                .IsUnique();
        base.OnModelCreating(modelBuilder);
    }
}