using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
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
}