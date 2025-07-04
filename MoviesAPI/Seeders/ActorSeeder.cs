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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Scarlett Johansson",
                Gender = "female",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Dwayne 'The Rock' Johnson",
                Gender = "male",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Tom Hanks",
                Gender = "male",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Jennifer Lawrence",
                Gender = "female",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Will Smith",
                Gender = "male",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Leonardo DiCaprio",
                Gender = "male",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Emma Watson",
                Gender = "female",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Ryan Reynolds",
                Gender = "male",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Name = "Natalie Portman",
                Gender = "female",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
    }
}
