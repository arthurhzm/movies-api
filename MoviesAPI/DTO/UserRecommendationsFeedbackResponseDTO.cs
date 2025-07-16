namespace MoviesAPI.DTO;

public class UserRecommendationsFeedbackResponseDTO
{
    public string MovieTitle { get; set; } = string.Empty;
    public string Feedback { get; set; } = string.Empty;
    public string? DetailedFeedback { get; set; }
}