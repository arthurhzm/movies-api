using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoviesAPI.Services;

namespace MoviesAPI.Controllers;

public class GenresController : ControllerBase
{
    private readonly GenresService _gendersService;
    public GenresController(GenresService gendersService)
    {
        _gendersService = gendersService;
    }

    [HttpGet("/genres")]
    [Authorize]
    public async Task<IActionResult> GetGenres()
    {
        var genres = await _gendersService.GetAllGenres();
        return Ok(new { Data = genres, Message = "Genres retrieved successfully." });

    }
}