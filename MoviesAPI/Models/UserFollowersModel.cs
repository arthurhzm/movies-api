namespace MoviesAPI.Models;

public class UserFollowersModel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int FollowerId { get; set; }
    public DateTime FollowedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}