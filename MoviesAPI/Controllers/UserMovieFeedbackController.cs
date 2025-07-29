using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoviesAPI.DTO;
using MoviesAPI.Services;

namespace MoviesAPI.Controllers;

public class UserMovieFeedbackController : ControllerBase
{
    private readonly UserMovieFeedbackService _userMovieFeedbackService;

    public UserMovieFeedbackController(UserMovieFeedbackService userMovieFeedbackService)
    {
        _userMovieFeedbackService = userMovieFeedbackService;
    }

    [HttpGet("user/{userId}/feedback")]
    [Authorize]
    public async Task<IActionResult> GetUserMovieFeedback(int userId, [FromQuery] string? movieTitle = null)
    {
        List<UserMovieFeedbackResponseDTO>? feedbackList;

        if (movieTitle != null)
        {
            var singleFeedback = await _userMovieFeedbackService.GetUserFeedbackByUserIdAndMovieTitle(userId, movieTitle);
            feedbackList = singleFeedback != null ? new List<UserMovieFeedbackResponseDTO> { singleFeedback } : null;
        }
        else
        {
            feedbackList = await _userMovieFeedbackService.GetUserFeedbacksByUserId(userId);
        }

        var feedback = feedbackList;

        if (feedback == null)
        {
            return NotFound("No feedback found.");
        }

        return Ok(new { Data = feedback, Message = "User movie feedback retrieved successfully." });

    }

    [HttpGet("user/{userId}/friends-feedback")]
    [Authorize]
    public async Task<IActionResult> GetFriendsMovieFeedback(int userId)
    {
        var feedback = await _userMovieFeedbackService.GetFriendsMoviesFeedback(userId);
        if (feedback == null)
        {
            return NoContent();
        }

        return Ok(new { Data = feedback, Message = "Friends movie feedback retrieved successfully." });
    }

    [HttpPost("user/{userId}/feedback")]
    [Authorize]
    public async Task<IActionResult> CreateUserMovieFeedback(int userId, [FromBody] CreateUserMovieFeedbackDTO feedback)
    {
        if (feedback == null)
        {
            return BadRequest("Invalid feedback data.");
        }

        var createdFeedback = await _userMovieFeedbackService.CreateUserMovieFeedback(userId, feedback);
        return Ok(new { Data = createdFeedback, Message = "User movie feedback created successfully." });
    }

    [HttpPatch("/feedback/{feedbackId}")]
    [Authorize]
    public async Task<IActionResult> UpdateUserMovieFeedback(int feedbackId, [FromBody] UpdateUserMovieFeedbackDTO feedback)
    {
        if (feedback == null)
        {
            return BadRequest("Invalid feedback data.");
        }

        var updatedFeedback = await _userMovieFeedbackService.UpdateUserMovieFeedback(feedbackId, feedback);
        if (updatedFeedback == null)
        {
            return NotFound("Feedback not found.");
        }

        return Ok(new { Data = updatedFeedback, Message = "User movie feedback updated successfully." });
    }

    [HttpDelete("/feedback/{feedbackId}")]
    [Authorize]
    public async Task<IActionResult> DeleteUserMovieFeedback(int feedbackId)
    {
        var deleted = await _userMovieFeedbackService.DeleteUserMovieFeedback(feedbackId);
        if (!deleted)
        {
            return NotFound("Feedback not found.");
        }

        return Ok(new { Message = "User movie feedback deleted successfully." });
    }
}