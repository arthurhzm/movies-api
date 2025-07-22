using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.DTO;
using MoviesAPI.Models;

namespace MoviesAPI.Services;

public class UserFollowersService
{
    public readonly AppDbContext _context;

    public UserFollowersService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<UserProfilePreviewResponseDTO>> GetFollowersAsync(int userId)
    {
        return await _context.UserFollowers
            .Where(f => f.UserId == userId)
            .Join(_context.Auth,
            f => f.FollowerId,
            auth => auth.Id,
            (f, auth) => new UserProfilePreviewResponseDTO(
                auth.Id,
                auth.Username,
                auth.ProfilePicture
            ))
            .ToListAsync();
    }

    public async Task<List<UserProfilePreviewResponseDTO>> GetFollowingAsync(int userId)
    {
        return await _context.UserFollowers
            .Where(f => f.FollowerId == userId)
            .Join(_context.Auth,
            f => f.UserId,
            auth => auth.Id,
            (f, auth) => new UserProfilePreviewResponseDTO(
                auth.Id,
                auth.Username,
                auth.ProfilePicture
            ))
            .ToListAsync();
    }

    public async Task<List<UserProfilePreviewResponseDTO>> GetFriendsAsync(int userId)
    {
        var followers = await GetFollowersAsync(userId);
        var following = await GetFollowingAsync(userId);

        var friends = followers.Intersect(following, new UserProfilePreviewResponseDTOComparer()).ToList();
        return friends;
    }

    public async Task<bool> FollowUserAsync(FollowUserDTO model)
    {
        if (model == null || model.UserId <= 0 || model.FollowerId <= 0)
        {
            throw new ArgumentException("Invalid user or follower ID.");
        }

        var existingFollow = await _context.UserFollowers
            .FirstOrDefaultAsync(f => f.UserId == model.UserId && f.FollowerId == model.FollowerId);

        if (existingFollow != null)
        {
            throw new InvalidOperationException("You are already following this user.");
        }

        var newFollow = new UserFollowersModel
        {
            UserId = model.UserId,
            FollowerId = model.FollowerId
        };

        _context.UserFollowers.Add(newFollow);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> UnfollowUserAsync(UnfollowUserDTO model)
    {
        if (model == null || model.UserId <= 0 || model.FollowerId <= 0)
        {
            throw new ArgumentException("Invalid user or follower ID.");
        }

        var existingFollow = await _context.UserFollowers
            .FirstOrDefaultAsync(f => f.UserId == model.UserId && f.FollowerId == model.FollowerId);

        if (existingFollow == null)
        {
            throw new InvalidOperationException("You are not following this user.");
        }

        _context.UserFollowers.Remove(existingFollow);
        await _context.SaveChangesAsync();

        return true;
    }
}