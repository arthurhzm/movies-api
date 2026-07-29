using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoviesAPI.DTO;
using MoviesAPI.Services;

namespace MoviesAPI.Controllers;

public class RecommendationController : ControllerBase
{
    private readonly RecommendationService _recommendationService;

    public RecommendationController(RecommendationService recommendationService)
    {
        _recommendationService = recommendationService;
    }

    [HttpGet("user/{userId}/recommendations")]
    [Authorize]
    public async Task<IActionResult> GetRecommendations(int userId, [FromQuery] bool special = false)
    {
        if (!IsCurrentUser(userId))
        {
            return Forbid();
        }

        try
        {
            var recommendations = await _recommendationService.GetRecommendationsAsync(userId, special);
            return Ok(new { data = recommendations, message = "Recomendações geradas com sucesso." });
        }
        catch (HuggingFaceGenerationException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "As recomendações estão indisponíveis no momento." });
        }
    }

    [HttpPost("user/{userId}/recommendations/roulette")]
    [Authorize]
    public async Task<IActionResult> GetRouletteRecommendation(int userId, CancellationToken cancellationToken)
    {
        if (!IsCurrentUser(userId))
        {
            return Forbid();
        }

        try
        {
            var recommendation = await _recommendationService.GetRouletteRecommendationAsync(userId, cancellationToken);
            return Ok(new { data = recommendation });
        }
        catch (HuggingFaceGenerationException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "A roleta está indisponível no momento." });
        }
    }

    private bool IsCurrentUser(int userId)
    {
        var claimValue = User.FindFirst(ClaimTypes.Name)?.Value;
        return int.TryParse(claimValue, out var authenticatedUserId) && authenticatedUserId == userId;
    }
}
