namespace MoviesAPI.DTO;

public class CreateUserDTO
{
    public string? Email { get; set; }
    public string? Password { get; set; }

    public CreateUserDTO(string? email, string? password)
    {
        Email = email;
        Password = password;
    }
}