namespace MoviesAPI.DTO;

public class RecommendationDTO
{
    public string MovieTitle { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string? WhyRecommend { get; set; }
    public List<string> StreamingServices { get; set; } = new();
    public bool IsSpecial { get; set; } = false;
}

public sealed class RouletteRecommendationDTO : RecommendationDTO
{
    public string? Overview { get; set; }
    public int? ConfidenceScore { get; set; }
    public List<string> PerfectMatchReasons { get; set; } = new();
}
