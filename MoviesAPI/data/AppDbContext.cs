using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
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
    public DbSet<UserMovieFeedbackModel> UserMovieFeedback { get; set; } = null!;
    public DbSet<UserRecommendationFeedbackModel> UserRecommendationFeedback { get; set; } = null!;

    public DbSet<UserFollowersModel> UserFollowers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthModel>()
                .HasIndex(u => u.Email)
                .IsUnique();

        modelBuilder.Entity<UserRecommendationFeedbackModel>()
        .ToTable(t => t.HasCheckConstraint("CK_UserRecommendationFeedback_Feedback",
            "\"Feedback\" IN ('like', 'superlike', 'dislike')"));

        base.OnModelCreating(modelBuilder);
    }
}