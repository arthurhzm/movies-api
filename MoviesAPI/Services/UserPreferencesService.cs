using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.DTO;
using MoviesAPI.Models;

namespace MoviesAPI.Services;

public class UserPreferencesService
{
    private readonly AppDbContext _context;

    public UserPreferencesService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UserPreferencesModel?> GetUserPreferencesByUserId(int userId)
    {
        return await _context.UserPreferences
            .FirstOrDefaultAsync(up => up.UserId == userId);
    }

    public async Task<UserPreferencesModel> CreateUserPreferences(int userId, CreateUserPreferencesDTO preferencesDto)
    {
        var preferences = new UserPreferencesModel
        {
            UserId = userId,
            FavoriteGenres = preferencesDto.Genres,
            FavoriteDirectors = preferencesDto.Directors,
            FavoriteActors = preferencesDto.Actors,
            MinReleaseYear = preferencesDto.MinReleaseYear,
            MaxDuration = preferencesDto.MaxDuration,
            AcceptAdultContent = preferencesDto.AcceptAdultContent
        };

        _context.UserPreferences.Add(preferences);
        await _context.SaveChangesAsync();

        return preferences;
    }
}