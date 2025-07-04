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

    [HttpGet]
    public async Task<IActionResult> GetGenders()
    {
        var genres = await _gendersService.GetAllGenders();
        return Ok(genres);
    }
}