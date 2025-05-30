using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;

namespace MoviesAPI.Controllers;

public class ActorsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ActorsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("/actors")]
    [Authorize]
    public async Task<IActionResult> GetAll()
    {
        var actors = await _context.Actors.ToListAsync();
        return Ok(new { Data = actors, Message = "Actors retrieved successfully." });
    }
}