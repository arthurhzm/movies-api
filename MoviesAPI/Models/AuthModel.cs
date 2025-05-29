namespace MoviesAPI.Models;

public class AuthModel
{
    public int Id { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? ApiKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}