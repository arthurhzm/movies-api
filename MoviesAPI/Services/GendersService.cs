using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.Models;

namespace MoviesAPI.Services;

public class GendersService
{
    private readonly AppDbContext _context;

    public GendersService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<GendersModel>> GetAllGenders()
    {
        return await _context.Genders.ToListAsync();
    }
}
