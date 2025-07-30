namespace MoviesAPI.DTO;

public class MovieUsersFeedbackResponseDTO
{
    public int Id { get; set; }

    public int UserId { get; set; }
    required public string Username { get; set; }

    public string? ProfilePicture { get; set; }
    public double Rating { get; set; }
    public string? Review { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

}