using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.Models;

namespace MoviesAPI.Services;

public class GenresService
{
    private readonly AppDbContext _context;

    public GenresService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<GenresModel>> GetAllGenres()
    {
        return await _context.Genres.ToListAsync();
    }
}
