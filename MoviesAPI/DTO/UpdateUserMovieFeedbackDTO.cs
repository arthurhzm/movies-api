namespace MoviesAPI.DTO;

public class UpdateUserMovieFeedbackDTO
{
    public double Rating { get; set; } = 0.0;
    public string? Review { get; set; }

    public UpdateUserMovieFeedbackDTO(double rating, string? review = null)
    {
        Rating = rating;
        Review = review;
    }
}