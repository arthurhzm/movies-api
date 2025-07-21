namespace MoviesAPI.DTO;

public class UnfollowUserDTO
{
    public int UserId { get; set; }
    public int FollowerId { get; set; }

    public UnfollowUserDTO(int userId, int followerId)
    {
        UserId = userId;
        FollowerId = followerId;
    }
}