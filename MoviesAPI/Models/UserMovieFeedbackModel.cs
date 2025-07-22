namespace MoviesAPI.Models;

public class UserMovieFeedbackModel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public double Rating { get; set; } = 0.0;
    public string? Review { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual AuthModel User { get; set; } = null!;
}