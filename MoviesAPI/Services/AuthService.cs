using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MoviesAPI.Data;
using MoviesAPI.DTO;
using MoviesAPI.Models;

namespace MoviesAPI.Services;

public class AuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;


    public AuthService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<AuthModel?> RegisterAsync(CreateUserDTO model)
    {
        if (model == null || string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
        {
            throw new ArgumentException("Email and Password are required.");
        }

        var existingUser = await _context.Auth.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (existingUser != null)
        {
            throw new InvalidOperationException("User already exists.");
        }

        var newUser = new AuthModel
        {
            Username = model.Username,
            Email = model.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(model.Password)
        };

        _context.Auth.Add(newUser);
        await _context.SaveChangesAsync();

        return newUser;
    }

    public async Task<(AuthModel user, string token, string refreshToken)> LoginAsync(AuthUserDTO model)
    {
        if (model == null || string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
        {
            throw new ArgumentException("Email and Password are required.");
        }

        var user = await _context.Auth.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
        {
            throw new Exception("Credenciais inválidas");
        }

        var token = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

        user.ApiKey = refreshToken;
        await _context.SaveChangesAsync();

        return (user, token, refreshToken);
    }

    public async Task<string> UpdatePasswordAsync(UpdatePasswordDTO model)
    {
        if (model == null || string.IsNullOrEmpty(model.NewPassword))
        {
            throw new ArgumentException("New password is required.");
        }

        var user = await _context.Auth.FirstOrDefaultAsync(x => x.Email == model.Email);
        if (user == null)
        {
            throw new Exception("User not found.");
        }

        if (user.ApiKey is null)
        {
            user.ApiKey = GenerateRefreshToken();
            await _context.SaveChangesAsync();
        }

        user.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
        await _context.SaveChangesAsync();
        return user.ApiKey;
    }

    public string GenerateJwtToken(AuthModel user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                    new Claim(ClaimTypes.Name, user.Id.ToString())
            }),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretKey!)), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);

    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public async Task<(AuthModel user, string newToken)> GetUserByRefreshToken(string refreshToken)
    {
        var user = await _context.Auth.FirstOrDefaultAsync(u => u.ApiKey == refreshToken);

        if (user == null)
        {
            throw new Exception("Token inválido");
        }

        var newToken = GenerateJwtToken(user);
        return (user, newToken);
    }
}