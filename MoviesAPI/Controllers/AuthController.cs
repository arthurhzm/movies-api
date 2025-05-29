using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> Create([FromBody] DTO.CreateUserDTO model)
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

}