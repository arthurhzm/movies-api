using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoviesAPI.Services;

namespace MoviesAPI.Controllers;

public class MatchController : ControllerBase
{
    private readonly MatchService _matchService;

    public MatchController(MatchService matchService)
    {
        _matchService = matchService;
    }

    [HttpPost("user/{userId}/match/{targetUserId}")]
    [Authorize]
    public async Task<IActionResult> GenerateMatch(int userId, int targetUserId)
    {
        try
        {
            var result = await _matchService.GenerateMatchAsync(userId, targetUserId);
            return Ok(new { data = result, message = "Match generated successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error generating match: {ex.Message}" });
        }
    }

    [HttpGet("user/{userId}/match/history")]
    [Authorize]
    public async Task<IActionResult> GetMatchHistory(int userId)
    {
        var history = await _matchService.GetMatchHistoryAsync(userId);
        return Ok(new { data = history, message = "Match history retrieved successfully." });
    }
}
