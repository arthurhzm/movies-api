namespace MoviesAPI.Models;

public class UserRecommendationFeedbackModel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string Feedback { get; set; } = string.Empty;
    public string? DetailedFeedback { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}