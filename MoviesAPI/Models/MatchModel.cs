namespace MoviesAPI.Models;

public class MatchModel
{
    public int Id { get; set; }
    public int UserId1 { get; set; }
    public int UserId2 { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public int? TmdbId { get; set; }
    public int? Year { get; set; }
    public string? WhyItWorks { get; set; }
    public int CompatibilityScore { get; set; }
    public string? CommonGenres { get; set; } // JSON array stored as string
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
