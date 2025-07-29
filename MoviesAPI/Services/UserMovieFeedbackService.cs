using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.DTO;
using MoviesAPI.Models;

namespace MoviesAPI.Services;

public class UserMovieFeedbackService
{
    private readonly AppDbContext _context;
    private readonly UserFollowersService _userFollowersService;

    public UserMovieFeedbackService(AppDbContext context, UserFollowersService userFollowersService)
    {
        _context = context;
        _userFollowersService = userFollowersService;
    }

    public async Task<List<UserMovieFeedbackResponseDTO>> GetUserFeedbacksByUserId(int userId)
    {
        var feedbacks = await _context.UserMovieFeedback
            .Where(umf => umf.UserId == userId)
            .OrderByDescending(f => f.UpdatedAt)
            .ToListAsync();

        return feedbacks.Select(feedback => new UserMovieFeedbackResponseDTO
        {
            Id = feedback.Id,
            MovieTitle = feedback.MovieTitle,
            Rating = feedback.Rating,
            Review = feedback.Review
        }).ToList();
    }

    public async Task<UserMovieFeedbackResponseDTO?> GetUserFeedbackByUserIdAndMovieTitle(int userId, string movieTitle)
    {
        var feedback = await _context.UserMovieFeedback
            .FirstOrDefaultAsync(umf => umf.UserId == userId && umf.MovieTitle == movieTitle);
        if (feedback == null)
        {
            return null;
        }

        return new UserMovieFeedbackResponseDTO
        {
            Id = feedback.Id,
            MovieTitle = feedback.MovieTitle,
            Rating = feedback.Rating,
            Review = feedback.Review
        };
    }

    public async Task<FriendMovieFeedbackDTO[]?> GetFriendsMoviesFeedback(int userId)
    {
        var friends = await _userFollowersService.GetFriendsAsync(userId);
        var friendIds = friends.Select(f => f.UserId).ToList();

        var feedbacks = await _context.UserMovieFeedback
            .Include(umf => umf.User)
            .Where(umf => friendIds.Contains(umf.UserId))
            .OrderByDescending(f => f.UpdatedAt)
            .ToListAsync();

        if (feedbacks.Count == 0)
        {
            return null;
        }

        return [.. feedbacks.Select(feedback => new FriendMovieFeedbackDTO
        {
            UserId = feedback.UserId,
            ProfilePicture = feedback.User.ProfilePicture,
            Username = feedback.User.Username,
            MovieTitle = feedback.MovieTitle,
            Rating = feedback.Rating,
            Review = feedback.Review!
        })];
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

    public async Task<UserMovieFeedbackResponseDTO?> UpdateUserMovieFeedback(int feedbackId, UpdateUserMovieFeedbackDTO feedbackDto)
    {
        var feedback = await _context.UserMovieFeedback.FindAsync(feedbackId);
        if (feedback == null)
        {
            return null;
        }

        feedback.Rating = feedbackDto.Rating;
        feedback.Review = feedbackDto.Review;
        feedback.UpdatedAt = DateTime.UtcNow;

        _context.UserMovieFeedback.Update(feedback);
        await _context.SaveChangesAsync();

        return new UserMovieFeedbackResponseDTO
        {
            Id = feedback.Id,
            MovieTitle = feedback.MovieTitle,
            Rating = feedback.Rating,
            Review = feedback.Review
        };
    }

    public async Task<bool> DeleteUserMovieFeedback(int feedbackId)
    {
        var feedback = await _context.UserMovieFeedback.FindAsync(feedbackId);
        if (feedback == null)
        {
            return false;
        }

        _context.UserMovieFeedback.Remove(feedback);
        await _context.SaveChangesAsync();

        return true;
    }
}