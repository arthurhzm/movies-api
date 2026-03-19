namespace MoviesAPI.Models;

public class GeneratedRecommendationModel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string? WhyRecommend { get; set; }
    public string? StreamingServices { get; set; } // JSON array stored as string
    public bool IsSpecial { get; set; } = false;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public virtual AuthModel User { get; set; } = null!;
}
