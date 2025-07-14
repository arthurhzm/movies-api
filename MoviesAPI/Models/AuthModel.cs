namespace MoviesAPI.Models;

public class AuthModel
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? ProfilePicture { get; set; }

    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? ApiKey { get; set; }

    public DateTime? LastLogin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}