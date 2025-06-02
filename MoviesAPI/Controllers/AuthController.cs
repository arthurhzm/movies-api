
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
            var (token, refreshToken) = await _authService.LoginAsync(model);
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

            return Ok(new { data = new { token } });
        }
        catch (Exception e)
        {
            return BadRequest(new { message = e.Message });
            throw;
        }
    }

    [HttpPut("update-password")]
    public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordDTO model)
    {
        try
        {
            Console.WriteLine($"Email: {model?.Email}, NewPassword: {model?.NewPassword}");
            var token = await _authService.UpdatePasswordAsync(model);
            return Ok(new { message = "Password updated successfully.", data = new { token } });
        }
        catch (Exception e)
        {
            return BadRequest(new { message = e.Message });
        }
    }
}