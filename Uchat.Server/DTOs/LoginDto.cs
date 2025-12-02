using System.ComponentModel.DataAnnotations;

namespace Uchat.Server.DTOs;

public class LoginDto
{
    [Required(ErrorMessage = "Username or Email is required")]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}
