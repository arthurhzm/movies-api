using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.Services;

namespace MoviesAPI.Controllers;
public class UserPreferencesController : ControllerBase
{
    private readonly UserPreferencesService _userPreferencesService;
    public UserPreferencesController(UserPreferencesService userPreferencesService)
    {
        _userPreferencesService = userPreferencesService;
    }

    [HttpGet("user/{userId}/preferences")]
    [Authorize]
    public async Task<IActionResult> GetUserPreferences(int userId)
    {
        var preferences = await _userPreferencesService.GetUserPreferencesByUserId(userId);
        if (preferences == null)
        {
            return NotFound();
        }
        return Ok(preferences);
    }

}
