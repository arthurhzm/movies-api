using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.DTO;
using MoviesAPI.Models;

namespace MoviesAPI.Services;

public class UserRecommendationFeedbackService
{
    private readonly AppDbContext _context;

    public UserRecommendationFeedbackService(AppDbContext context)
    {
        _context = context;
    }


    public async Task<List<UserRecommendationsFeedbackResponseDTO>> GetUserRecommendationsFeedback(int userId)
    {
        var feedbacks = await _context.UserRecommendationFeedback
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.UpdatedAt)
            .Select(f => new UserRecommendationsFeedbackResponseDTO
            {
                MovieTitle = f.MovieTitle,
                Feedback = f.Feedback
            })
            .ToListAsync();

        return feedbacks;
    }
    public async Task<UserRecommendationFeedbackModel> PutUserRecommendationFeedback(PutUserRecommendationFeedbackDTO feedback)
    {
        var existingFeedback = await _context.UserRecommendationFeedback
            .FirstOrDefaultAsync(f => f.UserId == feedback.UserId && f.MovieTitle == feedback.MovieTitle);
        if (existingFeedback == null)
        {
            existingFeedback = new UserRecommendationFeedbackModel
            {
                UserId = feedback.UserId,
                MovieTitle = feedback.MovieTitle,
                Feedback = feedback.Feedback,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.UserRecommendationFeedback.Add(existingFeedback);
        }
        else
        {
            existingFeedback.Feedback = feedback.Feedback;
            existingFeedback.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return existingFeedback;
    }
}