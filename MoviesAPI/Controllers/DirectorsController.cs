using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;

namespace MoviesAPI.Controllers;

public class DirectorsController : ControllerBase
{
    private readonly AppDbContext _context;

    public DirectorsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("/directors")]
    [Authorize]
    public async Task<IActionResult> GetAll()
    {
        var directors = await _context.Directors.ToListAsync();
        return Ok(new { Data = directors, Message = "Directors retrieved successfully." });
    }
}