using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
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

    public async Task<AuthModel?> UpdateUserAsync(string userId, UpdateUserDTO model)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID is required.");
        }

        var user = await _context.Auth.FindAsync(int.Parse(userId));
        if (user == null)
        {
            throw new Exception("User not found.");
        }

        if (!string.IsNullOrEmpty(model.Username))
        {
            user.Username = model.Username;
        }

        if (!string.IsNullOrEmpty(model.Email))
        {
            var existingUser = await _context.Auth.FirstOrDefaultAsync(u => u.Email == model.Email && u.Id != user.Id);
            if (existingUser != null)
            {
                throw new InvalidOperationException("Email already exists.");
            }
            user.Email = model.Email;
        }

        if (!string.IsNullOrEmpty(model.Password))
        {
            user.Password = BCrypt.Net.BCrypt.HashPassword(model.Password);
        }

        if (!string.IsNullOrEmpty(model.BirthDate))
        {
            user.BirthDate = DateTime.SpecifyKind(DateTime.Parse(model.BirthDate), DateTimeKind.Utc);
        }

        if (!string.IsNullOrEmpty(model.ProfilePicture))
        {
            user.ProfilePicture = model.ProfilePicture;
        }

        if (!string.IsNullOrEmpty(model.Gender))
        {
            user.Gender = model.Gender;
        }

        await _context.SaveChangesAsync();
        return user;
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

    public async Task<UserProfileResponseDTO?> GetUserByIdAsync(string userId)
    {
        var user = await _context.Auth.FindAsync(int.Parse(userId));
        if (user == null)
        {
            return null;
        }

        return new UserProfileResponseDTO(
            user.BirthDate,
            user.Gender,
            user.ProfilePicture,
            user.LastLogin
        );
    }

    public async Task<UserProfilePreviewResponseDTO[]?> GetUsersByUsername(string username)
    {
        var users = await _context.Auth.Where(u => EF.Functions.Like(
            u.Username.ToUpper().Trim(),
            $"%{username.ToUpper().Trim()}%")).ToListAsync();
        if (users == null || users.Count == 0)
        {
            return null;
        }
        return [.. users.Select(u => new UserProfilePreviewResponseDTO(u.Id, u.Email, u.Username, u.ProfilePicture))];
    }

    public async Task<UserProfilePreviewResponseDTO?> GetUserByEmailAsync(string email)
    {
        var user = await _context.Auth.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            return null;
        }

        return new UserProfilePreviewResponseDTO(user.Id, user.Email, user.Username, user.ProfilePicture);
    }

    public async Task<bool> ForgotPasswordAsync(string email)
    {
        var user = await _context.Auth.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            return false;
        }


        var random = new Random();
        var code = random.Next(100000, 999999).ToString();

        user.RecoveryToken = code;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var fromAddress = new MailAddress(Environment.GetEnvironmentVariable("SMTP_EMAIL")!, "CineMatch");
        var toAddress = new MailAddress(email);
        var fromPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD")!;
        const string Subject = "CineMatch - Recuperação de Senha";
        string Body = $"Olá {user.Username},\n\n" +
                      "Você solicitou a recuperação de senha. Use o código abaixo para redefinir sua senha:\n\n" +
                      $"{code}\n\n" +
                      "Se você não solicitou essa recuperação, ignore este e-mail.\n\n" +
                      "Atenciosamente,\nCineMatch";
        var smtp = new SmtpClient
        {
            Host = "smtp.gmail.com",
            Port = 587,
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
        };

        using (var message = new MailMessage(fromAddress, toAddress)
        {
            Subject = Subject,
            Body = Body
        })
        {
            try
            {
                smtp.Send(message);
                Console.WriteLine("Email enviado com sucesso!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao enviar email: " + ex.Message);
                throw new Exception("Erro ao enviar e-mail de recuperação de senha: " + ex.Message);
            }
        }

        return true;
    }


}