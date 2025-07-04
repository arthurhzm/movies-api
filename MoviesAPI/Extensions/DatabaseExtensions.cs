using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.Seeders;

namespace MoviesAPI.Extensions;

public static class DatabaseExtensions
{
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await context.Database.MigrateAsync();

            await SeedDirectorsAsync(context);
            await SeedActorsAsync(context);
            await SeedGenresAsync(context);

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Erro ao executar migrações do banco de dados");
            throw;
        }
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

    private static async Task SeedGenresAsync(AppDbContext context)
    {
        if (context.Genres.Any())
            return;

        var genres = GenresSeeder.GetFamousGenres();
        await context.Genres.AddRangeAsync(genres);
    }
}