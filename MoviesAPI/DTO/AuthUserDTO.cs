namespace MoviesAPI.DTO;

public class AuthUserDTO
{
    public string Email { get; set; }
    public string Password { get; set; }

    public AuthUserDTO(string email, string password)
    {
        Email = email;
        Password = password;
    }
}