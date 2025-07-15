namespace MoviesAPI.DTO;

public class CreateUserMovieFeedbackDTO
{
    public int UserId { get; set; }
    required public string MovieTitle { get; set; }
    public double Rating { get; set; }
    public string? Review { get; set; }
}
