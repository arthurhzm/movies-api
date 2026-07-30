namespace MoviesAPI.DTO;

public class UserMovieFeedbackResponseDTO
{
    public int Id { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public int? TmdbId { get; set; }
    public double Rating { get; set; }
    public string? Review { get; set; }
}