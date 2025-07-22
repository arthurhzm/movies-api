
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoviesAPI.DTO;
using MoviesAPI.Services;


namespace MoviesAPI.Controllers;

public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly AuthService _authService;

    public AuthController(IConfiguration configuration, AuthService authService)
    {
        _configuration = configuration;
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Create([FromBody] CreateUserDTO model)
    {
        try
        {
            var user = await _authService.RegisterAsync(model);
            if (user == null)
            {
                return BadRequest("User registration failed.");
            }

            return Ok(new { Data = user, Message = "User registered successfully." });
        }
        catch (Exception e)
        {
            return BadRequest(new { message = e.Message });
            throw;
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AuthUserDTO model)
    {
        try
        {
            var (user, token, refreshToken) = await _authService.LoginAsync(model);
            if (token == null || refreshToken == null)
            {
                return BadRequest("User login failed.");
            }

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddDays(7),
            };

            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);

            return Ok(new { data = new { token, user } });
        }
        catch (Exception e)
        {
            return BadRequest(new { message = e.Message });
            throw;
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Expires = DateTime.UtcNow.AddDays(-1),
        };

        Response.Cookies.Append("refreshToken", "", cookieOptions);

        return Ok(new { message = "Logout successful." });
    }

    [HttpPut("update-password")]
    public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordDTO model)
    {
        try
        {
            Console.WriteLine($"Email: {model?.Email}, NewPassword: {model?.NewPassword}");
            var token = await _authService.UpdatePasswordAsync(model!);
            return Ok(new { message = "Password updated successfully.", data = new { token } });
        }
        catch (Exception e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken()
    {
        var refreshToken = Request.Cookies["refreshToken"];

        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { message = "Token inválido" });
        }

        var (user, token) = await _authService.GetUserByRefreshToken(refreshToken);
        return Ok(new { data = new { token, user } });
    }

    [HttpPatch("/users/{userId}")]
    [Authorize]
    public async Task<IActionResult> UpdateUser(string userId, [FromBody] UpdateUserDTO model)
    {
        if (model == null)
        {
            return BadRequest("Invalid user data.");
        }

        var updatedUser = await _authService.UpdateUserAsync(userId, model);
        if (updatedUser == null)
        {
            return NotFound("User not found.");
        }

        return Ok(new { Data = updatedUser, Message = "User updated successfully." });
    }

    [HttpGet("/users/{userId}")]
    [Authorize]
    public async Task<IActionResult> GetUserById(string userId)
    {
        var user = await _authService.GetUserByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        return Ok(new { data = user });
    }

    [HttpGet("/users")]
    [Authorize]
    public async Task<IActionResult> GetUsersByUsername([FromQuery] string username)
    {
        var users = await _authService.GetUsersByUsername(username);
        if (users == null || users.Length == 0)
        {
            return NotFound(new { message = "No users found." });
        }

        return Ok(new { data = users });
    }
}