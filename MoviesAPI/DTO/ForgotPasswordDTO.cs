namespace MoviesAPI.DTO;

public class ForgotPasswordDTO
{
    public string Email { get; set; }

    public ForgotPasswordDTO(string email)
    {
        Email = email;
    }
}