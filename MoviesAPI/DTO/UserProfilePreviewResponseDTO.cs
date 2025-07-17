namespace MoviesAPI.DTO;

public class UserProfilePreviewResponseDTO
{
    public int UserId { get; set; }
    public string Username { get; set; }
    public string? ProfilePicture { get; set; }

    public UserProfilePreviewResponseDTO(int userId, string username, string? profilePicture)
    {
        UserId = userId;
        Username = username;
        ProfilePicture = profilePicture;
    }
}