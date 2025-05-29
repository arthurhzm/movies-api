using MoviesAPI.Data;
using MoviesAPI.Seeders;

namespace MoviesAPI.Extensions;

public static class DatabaseExtensions
{
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await context.Database.EnsureCreatedAsync();

        await SeedDirectorsAsync(context);

        await context.SaveChangesAsync();
    }

    private static async Task SeedDirectorsAsync(AppDbContext context)
    {
        if (context.Directors.Any())
            return;

        var directors = DirectorSeeder.GetFamousDirectors();
        await context.Directors.AddRangeAsync(directors);
    }
}