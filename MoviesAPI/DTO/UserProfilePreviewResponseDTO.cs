namespace MoviesAPI.DTO;

public class UserProfilePreviewResponseDTO
{
    public int UserId { get; set; }
    public string? Email { get; set; }
    public string Username { get; set; }
    public string? ProfilePicture { get; set; }

    public UserProfilePreviewResponseDTO(int userId, string? email, string username, string? profilePicture)
    {
        UserId = userId;
        Email = email;
        Username = username;
        ProfilePicture = profilePicture;
    }
}