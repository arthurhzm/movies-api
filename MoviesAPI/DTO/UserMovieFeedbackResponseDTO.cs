namespace MoviesAPI.DTO;

public class UserMovieFeedbackResponseDTO
{
    public int Id { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public double Rating { get; set; }
    public string? Review { get; set; }
}