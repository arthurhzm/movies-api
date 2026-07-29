using System.Security.Claims;
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
        if (!IsCurrentUser(userId))
        {
            return Forbid();
        }

        try
        {
            var result = await _matchService.GenerateMatchAsync(userId, targetUserId);
            return Ok(new { data = result, message = "Match gerado com sucesso." });
        }
        catch (HuggingFaceGenerationException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "O match está indisponível no momento." });
        }
    }

    [HttpGet("user/{userId}/match/history")]
    [Authorize]
    public async Task<IActionResult> GetMatchHistory(int userId)
    {
        if (!IsCurrentUser(userId))
        {
            return Forbid();
        }

        var history = await _matchService.GetMatchHistoryAsync(userId);
        return Ok(new { data = history, message = "Histórico de matches recuperado com sucesso." });
    }

    private bool IsCurrentUser(int userId)
    {
        var claimValue = User.FindFirst(ClaimTypes.Name)?.Value;
        return int.TryParse(claimValue, out var authenticatedUserId) && authenticatedUserId == userId;
    }
}
