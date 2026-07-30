namespace MoviesAPI.DTO;

public class MatchResultDTO
{
    public string MovieTitle { get; set; } = string.Empty;
    public int? TmdbId { get; set; }
    public int? Year { get; set; }
    public string? WhyItWorks { get; set; }
    public int CompatibilityScore { get; set; }
    public List<string> CommonGenres { get; set; } = new();
    public List<string> CommonDirectors { get; set; } = new();
}

public class MatchHistoryDTO
{
    public int Id { get; set; }
    public int UserId1 { get; set; }
    public int UserId2 { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public int? TmdbId { get; set; }
    public int? Year { get; set; }
    public string? WhyItWorks { get; set; }
    public int CompatibilityScore { get; set; }
    public List<string> CommonGenres { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}
