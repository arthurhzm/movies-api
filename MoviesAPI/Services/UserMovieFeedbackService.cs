using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.DTO;
using MoviesAPI.Models;

namespace MoviesAPI.Services;

public class UserMovieFeedbackService
{
    private readonly AppDbContext _context;

    public UserMovieFeedbackService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UserMovieFeedbackModel?> GetUserFeedbacksByUserId(int userId)
    {
        return await _context.UserMovieFeedback
            .FirstOrDefaultAsync(umf => umf.UserId == userId);
    }

    public async Task<UserMovieFeedbackModel?> GetUserFeedbackByUserIdAndMovieTitle(int userId, string movieTitle)
    {
        return await _context.UserMovieFeedback
            .FirstOrDefaultAsync(umf => umf.UserId == userId && umf.MovieTitle == movieTitle);
    }
    
    public async Task<UserMovieFeedbackModel> CreateUserMovieFeedback(int userId, CreateUserMovieFeedbackDTO feedbackDto)
    {
        var feedback = new UserMovieFeedbackModel
        {
            UserId = userId,
            MovieTitle = feedbackDto.MovieTitle,
            Rating = feedbackDto.Rating,
            Review = feedbackDto.Review,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.UserMovieFeedback.Add(feedback);
        await _context.SaveChangesAsync();

        return feedback;
    }
}