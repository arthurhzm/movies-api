using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.Seeders;

namespace MoviesAPI.Extensions;

public static class DatabaseExtensions
{
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await context.Database.MigrateAsync();

        await SeedDirectorsAsync(context);
        await SeedActorsAsync(context);

        await context.SaveChangesAsync();
    }

    private static async Task SeedDirectorsAsync(AppDbContext context)
    {
        if (context.Directors.Any())
            return;

        var directors = DirectorSeeder.GetFamousDirectors();
        await context.Directors.AddRangeAsync(directors);
    }

    private static async Task SeedActorsAsync(AppDbContext context)
    {
        if (context.Actors.Any())
            return;

        var actors = ActorSeeder.GetFamousActors();
        await context.Actors.AddRangeAsync(actors);
    }
}