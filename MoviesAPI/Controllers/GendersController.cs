using Microsoft.AspNetCore.Mvc;
using MoviesAPI.Services;

namespace MoviesAPI.Controllers;

public class GendersController : ControllerBase
{
    private readonly GendersService _gendersService;
    public GendersController(GendersService gendersService)
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