namespace MoviesAPI.DTO;

public class FollowUserDTO
{
    public int UserId { get; set; }
    public int FollowerId { get; set; }

    public FollowUserDTO(int userId, int followerId)
    {
        UserId = userId;
        FollowerId = followerId;
    }
}