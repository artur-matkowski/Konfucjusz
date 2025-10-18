

using System.ComponentModel.DataAnnotations;

public class LoginViewModel
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Podaj nazwę użytkownika")]
    public string? UserName { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Podaj hasło")]
    public string? Password { get; set; }
}