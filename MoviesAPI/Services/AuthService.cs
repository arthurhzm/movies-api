using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.DTO;
using MoviesAPI.Models;

namespace MoviesAPI.Services;

public class AuthService
{
    private readonly AppDbContext _context;

    public AuthService(AppDbContext context)
    {
        _context = context;
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
            Email = model.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(model.Password)
        };

        _context.Auth.Add(newUser);
        await _context.SaveChangesAsync();

        return newUser;
    }
}