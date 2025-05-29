using MoviesAPI.Models;

namespace MoviesAPI.Seeders;

public class DirectorSeeder
{
    public static List<DirectorsModel> GetFamousDirectors()
    {
        return
        [
            new() {
                Name = "Steven Spielberg",
                PopularityScore = 9.8f,
            CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Martin Scorsese", // cinema
                PopularityScore = 9.7f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Christopher Nolan",
                PopularityScore = 9.6f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Quentin Tarantino",
                PopularityScore = 9.5f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Alfred Hitchcock",
                PopularityScore = 9.4f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Stanley Kubrick",
                PopularityScore = 9.3f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Francis Ford Coppola",
                PopularityScore = 9.2f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Ridley Scott",
                PopularityScore = 9.1f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "James Cameron",
                PopularityScore = 9.0f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Peter Jackson",
                PopularityScore = 8.9f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        ];
    }
}
