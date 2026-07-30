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

        return feedbacks.Select(ToResponseDTO).ToList();
    }

    public async Task<UserMovieFeedbackResponseDTO?> GetUserFeedbackByUserIdAndMovieTitle(int userId, string movieTitle)
    {
        var feedback = await _context.UserMovieFeedback
            .FirstOrDefaultAsync(umf => umf.UserId == userId && umf.MovieTitle == movieTitle);
        return feedback == null ? null : ToResponseDTO(feedback);
    }

    public async Task<UserMovieFeedbackResponseDTO?> GetUserFeedbackByUserIdAndTmdbId(int userId, int tmdbId)
    {
        var feedback = await _context.UserMovieFeedback
            .FirstOrDefaultAsync(umf => umf.UserId == userId && umf.TmdbId == tmdbId);
        return feedback == null ? null : ToResponseDTO(feedback);
    }

    private static UserMovieFeedbackResponseDTO ToResponseDTO(UserMovieFeedbackModel feedback) => new()
    {
        Id = feedback.Id,
        MovieTitle = feedback.MovieTitle,
        TmdbId = feedback.TmdbId,
        Rating = feedback.Rating,
        Review = feedback.Review
    };

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
        // Upsert by stable TMDB id so rating a movie that was already imported
        // updates the existing row instead of creating a duplicate.
        UserMovieFeedbackModel? feedback = null;
        if (feedbackDto.TmdbId is int tmdbId)
        {
            feedback = await _context.UserMovieFeedback
                .FirstOrDefaultAsync(umf => umf.UserId == userId && umf.TmdbId == tmdbId);
        }

        if (feedback == null)
        {
            feedback = new UserMovieFeedbackModel
            {
                UserId = userId,
                MovieTitle = feedbackDto.MovieTitle,
                TmdbId = feedbackDto.TmdbId,
                Rating = feedbackDto.Rating,
                Review = feedbackDto.Review,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.UserMovieFeedback.Add(feedback);
        }
        else
        {
            feedback.MovieTitle = feedbackDto.MovieTitle;
            feedback.Rating = feedbackDto.Rating;
            feedback.Review = feedbackDto.Review;
            feedback.UpdatedAt = DateTime.UtcNow;
        }

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

        return ToResponseDTO(feedback);
    }

    public async Task<int?> GetUserIdByFeedbackIdAsync(int feedbackId)
    {
        return await _context.UserMovieFeedback
            .Where(f => f.Id == feedbackId)
            .Select(f => (int?)f.UserId)
            .FirstOrDefaultAsync();
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

    public async Task<MovieUsersFeedbackResponseDTO[]> GetMovieUsersFeedbackByTmdbId(int tmdbId)
    {
        var feedbacks = await _context.UserMovieFeedback
            .Include(umf => umf.User)
            .Where(umf => umf.TmdbId == tmdbId)
            .OrderByDescending(f => f.UpdatedAt)
            .ToListAsync();

        return MapMovieUsersFeedback(feedbacks);
    }

    public async Task<MovieUsersFeedbackResponseDTO[]> GetMovieUsersFeedback(string movieTitle)
    {
        var feedbacks = await _context.UserMovieFeedback
            .Include(umf => umf.User)
            .Where(umf => umf.MovieTitle == movieTitle)
            .OrderByDescending(f => f.UpdatedAt)
            .ToListAsync();

        return MapMovieUsersFeedback(feedbacks);
    }

    private static MovieUsersFeedbackResponseDTO[] MapMovieUsersFeedback(List<UserMovieFeedbackModel> feedbacks)
    {
        return feedbacks.Select(feedback => new MovieUsersFeedbackResponseDTO
        {
            Id = feedback.Id,
            UserId = feedback.UserId,
            Username = feedback.User.Username,
            ProfilePicture = feedback.User.ProfilePicture,
            Rating = feedback.Rating,
            Review = feedback.Review,
            UpdatedAt = feedback.UpdatedAt
        }).ToArray();
    }
}