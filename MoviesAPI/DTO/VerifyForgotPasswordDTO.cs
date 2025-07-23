namespace MoviesAPI.DTO;

public class VerifyForgotPasswordDTO
{
    public string Email { get; set; }
    public string Code { get; set; }

    public VerifyForgotPasswordDTO(string email, string code)
    {
        Email = email;
        Code = code;
    }
}