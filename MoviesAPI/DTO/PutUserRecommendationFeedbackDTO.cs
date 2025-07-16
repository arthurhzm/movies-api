namespace MoviesAPI.DTO;

public class PutUserRecommendationFeedbackDTO
{
    public int UserId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string Feedback { get; set; } = string.Empty;
}