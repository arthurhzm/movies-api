using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoviesAPI.DTO;
using MoviesAPI.Services;

namespace MoviesAPI.Controllers;

public class UserFollowersController : ControllerBase
{
    private readonly UserFollowersService _userFollowersService;

    public UserFollowersController(UserFollowersService userFollowersService)
    {
        _userFollowersService = userFollowersService;
    }

    [HttpGet("followers/{userId}")]
    [Authorize]
    public async Task<IActionResult> GetFollowers(int userId)
    {
        try
        {
            var followers = await _userFollowersService.GetFollowersAsync(userId);
            if (followers == null)
            {
                return NotFound("No followers found for this user.");
            }

            return Ok(new { Data = followers });
        }
        catch (Exception e)
        {
            return BadRequest(new { message = e.Message });
        }
    }


    [HttpPost("follow")]
    [Authorize]
    public async Task<IActionResult> FollowUser([FromBody] FollowUserDTO model)
    {
        try
        {
            var result = await _userFollowersService.FollowUserAsync(model);
            if (!result)
            {
                return BadRequest("Failed to follow user.");
            }

            return Ok(new { Message = "User followed successfully." });
        }
        catch (Exception e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPost("unfollow")]
    [Authorize]
    public async Task<IActionResult> UnfollowUser([FromBody] UnfollowUserDTO model)
    {
        try
        {
            var result = await _userFollowersService.UnfollowUserAsync(model);
            if (!result)
            {
                return BadRequest("Failed to unfollow user.");
            }

            return Ok(new { Message = "User unfollowed successfully." });
        }
        catch (Exception e)
        {
            return BadRequest(new { message = e.Message });
        }
    }
}