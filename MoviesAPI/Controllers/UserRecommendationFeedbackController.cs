

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoviesAPI.DTO;
using MoviesAPI.Services;

namespace MoviesAPI.Controllers;

public class UserRecommendationFeedbackController : ControllerBase
{
    private readonly UserRecommendationFeedbackService _userRecommendationFeedbackService;

    public UserRecommendationFeedbackController(UserRecommendationFeedbackService userRecommendationFeedbackService)
    {
        _userRecommendationFeedbackService = userRecommendationFeedbackService;
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
        return Ok(new { Data = result, Message = "Feedback on recommendations updated successfully." });
    }
}