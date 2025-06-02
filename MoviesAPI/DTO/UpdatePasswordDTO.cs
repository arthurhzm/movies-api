namespace MoviesAPI.DTO;

public class UpdatePasswordDTO
{
    public string Email { get; set; }
    public string NewPassword { get; set; }

    public UpdatePasswordDTO(string email, string newPassword)
    {
        Email = email;
        NewPassword = newPassword;
    }
}