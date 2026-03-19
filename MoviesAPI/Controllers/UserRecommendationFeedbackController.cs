

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoviesAPI.DTO;
using MoviesAPI.Services;

namespace MoviesAPI.Controllers;

public class UserRecommendationFeedbackController : ControllerBase
{
    private readonly UserRecommendationFeedbackService _userRecommendationFeedbackService;
    private readonly RecommendationService _recommendationService;

    public UserRecommendationFeedbackController(
        UserRecommendationFeedbackService userRecommendationFeedbackService,
        RecommendationService recommendationService)
    {
        _userRecommendationFeedbackService = userRecommendationFeedbackService;
        _recommendationService = recommendationService;
    }

    [HttpGet("user/{userId}/recommendations/feedback")]
    [Authorize]
    public async Task<IActionResult> GetUserRecommendationFeedback(int userId)
    {
        var feedback = await _userRecommendationFeedbackService.GetUserRecommendationsFeedback(userId);
        return Ok(new { Data = feedback, Message = "User recommendations feedback retrieved successfully." });
    }

    [HttpPut("/recommendations/feedback")]
    [Authorize]
    public async Task<IActionResult> PutUserRecommendationFeedback([FromBody] PutUserRecommendationFeedbackDTO feedback)
    {
        var result = await _userRecommendationFeedbackService.PutUserRecommendationFeedback(feedback);

        // Invalidate cache so next request regenerates with the new feedback
        await _recommendationService.InvalidateCacheAsync(feedback.UserId);

        return Ok(new { Data = result, Message = "Feedback on recommendations updated successfully." });
    }
}