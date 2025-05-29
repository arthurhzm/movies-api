using Microsoft.AspNetCore.Mvc;

namespace MoviesAPI.Controllers;

public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] DTO.CreateUserDTO model)
    {
        if (model == null || string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
        {
            return BadRequest("Invalid registration data.");
        }

        return Ok(new { message = "User registered successfully", user = model });
    }

}