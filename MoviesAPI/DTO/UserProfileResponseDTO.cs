namespace MoviesAPI.DTO;

public class UserProfileResponseDTO
{
    public DateTime? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? ProfilePicture { get; set; }
    public DateTime? LastLogin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserProfileResponseDTO(DateTime? birthDate, string? gender, string? profilePicture, DateTime? lastLogin)
    {
        BirthDate = birthDate;
        Gender = gender;
        ProfilePicture = profilePicture;
        LastLogin = lastLogin;
        CreatedAt = DateTime.UtcNow;
    }
}