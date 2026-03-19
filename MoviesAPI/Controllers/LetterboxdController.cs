using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoviesAPI.Data;
using MoviesAPI.Services;

namespace MoviesAPI.Controllers;

public class LetterboxdController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly LetterboxdService _letterboxdService;

    public LetterboxdController(AppDbContext context, LetterboxdService letterboxdService)
    {
        _context = context;
        _letterboxdService = letterboxdService;
    }

    [HttpPut("user/{userId}/letterboxd/connect")]
    [Authorize]
    public async Task<IActionResult> Connect(int userId, [FromBody] ConnectLetterboxdRequest request)
    {
        var user = await _context.Auth.FindAsync(userId);
        if (user == null) return NotFound(new { message = "User not found." });

        user.LetterboxdUsername = request.Username;
        await _context.SaveChangesAsync();

        // Trigger first sync
        var imported = await _letterboxdService.SyncUserAsync(userId);

        return Ok(new { data = new { username = user.LetterboxdUsername, lastSync = user.LetterboxdLastSync, imported }, message = "Letterboxd connected and synced." });
    }

    [HttpPost("user/{userId}/letterboxd/sync")]
    [Authorize]
    public async Task<IActionResult> Sync(int userId)
    {
        var user = await _context.Auth.FindAsync(userId);
        if (user == null) return NotFound(new { message = "User not found." });
        if (string.IsNullOrEmpty(user.LetterboxdUsername))
            return BadRequest(new { message = "No Letterboxd username connected." });

        var imported = await _letterboxdService.SyncUserAsync(userId);
        user = await _context.Auth.FindAsync(userId);

        return Ok(new { data = new { imported, lastSync = user!.LetterboxdLastSync }, message = "Sync completed." });
    }

    [HttpDelete("user/{userId}/letterboxd/disconnect")]
    [Authorize]
    public async Task<IActionResult> Disconnect(int userId)
    {
        var user = await _context.Auth.FindAsync(userId);
        if (user == null) return NotFound(new { message = "User not found." });

        user.LetterboxdUsername = null;
        user.LetterboxdLastSync = null;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Letterboxd disconnected." });
    }

    [HttpGet("user/{userId}/letterboxd/status")]
    [Authorize]
    public async Task<IActionResult> Status(int userId)
    {
        var user = await _context.Auth.FindAsync(userId);
        if (user == null) return NotFound(new { message = "User not found." });

        var totalMovies = await _letterboxdService.GetImportedCountAsync(userId);

        return Ok(new
        {
            data = new
            {
                username = user.LetterboxdUsername,
                lastSync = user.LetterboxdLastSync,
                totalMovies
            }
        });
    }
}

public record ConnectLetterboxdRequest(string Username);
