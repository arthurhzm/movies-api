namespace MoviesAPI.DTO;

public class FriendMovieFeedbackDTO
{
    public int UserId { get; set; }
    public string? ProfilePicture { get; set; }
    public string Username { get; set; } = string.Empty;

    required public string MovieTitle { get; set; }
    public double Rating { get; set; }
    required public string Review { get; set; }
}
