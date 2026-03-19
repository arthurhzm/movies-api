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
        try
        {
            var recommendations = await _recommendationService.GetRecommendationsAsync(userId, special);
            return Ok(new { data = recommendations, message = "Recommendations generated successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error generating recommendations: {ex.Message}" });
        }
    }
}
