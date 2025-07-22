namespace MoviesAPI.DTO;

public class UpdateUserDTO
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? BirthDate { get; set; }

    public string? ProfilePicture { get; set; } // base64

    public string? Gender { get; set; }

    public UpdateUserDTO(string? username, string? email, string? password, string? birthDate, string? profilePicture, string? gender)
    {
        Username = username;
        Email = email;
        Password = password;
        BirthDate = birthDate;
        ProfilePicture = profilePicture;
        Gender = gender;
    }

}