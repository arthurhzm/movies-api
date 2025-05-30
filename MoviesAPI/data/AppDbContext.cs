using Microsoft.EntityFrameworkCore;
using MoviesAPI.Models;

namespace MoviesAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AuthModel> Auth { get; set; } = null!;
    public DbSet<ActorsModel> Actors { get; set; } = null!;
    public DbSet<DirectorsModel> Directors { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (string.IsNullOrEmpty(databaseUrl))
                throw new InvalidOperationException("Database connection string is not configured.");

            string connectionString;
            if (databaseUrl.StartsWith("postgresql://"))
            {
                // Converter formato URI para connection string
                var uri = new Uri(databaseUrl);
                connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={uri.UserInfo.Split(':')[0]};Password={uri.UserInfo.Split(':')[1]};SSL Mode=Require;Trust Server Certificate=true";
            }
            else
            {
                connectionString = databaseUrl;
            }

            optionsBuilder.UseNpgsql(connectionString);
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