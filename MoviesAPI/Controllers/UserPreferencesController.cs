using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.DTO;
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


    [HttpPost("user/{userId}/preferences")]
    [Authorize]
    public async Task<IActionResult> CreateUserPreferences(int userId, [FromBody] CreateUserPreferencesDTO preferences)
    {
        if (preferences == null)
        {
            return BadRequest("Invalid preferences data.");
        }

        if (preferences.Genres == null || preferences.Genres.Count == 0)
        {
            return BadRequest("At least one genre must be specified.");
        }

        var createdPreferences = await _userPreferencesService.CreateUserPreferences(userId, preferences);
        return Ok(new { Data = createdPreferences, Message = "User preferences created successfully." });
    }

}
