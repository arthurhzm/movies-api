using MoviesAPI.Models;
using System;
using System.Collections.Generic;

namespace MoviesAPI.Seeders;

public class ActorSeeder
{
    public static List<ActorsModel> GetFamousActors()
    {
        return new List<ActorsModel>
        {
            new() {
                Name = "Robert Downey Jr.",
                Gender = "male",
                PopularityScore = 9.8f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Scarlett Johansson",
                Gender = "female",
                PopularityScore = 9.7f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Dwayne 'The Rock' Johnson",
                Gender = "male",
                PopularityScore = 9.6f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Tom Hanks",
                Gender = "male",
                PopularityScore = 9.5f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Jennifer Lawrence",
                Gender = "female",
                PopularityScore = 9.4f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Will Smith",
                Gender = "male",
                PopularityScore = 9.3f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Leonardo DiCaprio",
                Gender = "male",
                PopularityScore = 9.2f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Emma Watson",
                Gender = "female",
                PopularityScore = 9.1f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Ryan Reynolds",
                Gender = "male",
                PopularityScore = 9.0f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Natalie Portman",
                Gender = "female",
                PopularityScore = 8.9f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
    }
}
