using System.ComponentModel.DataAnnotations;

namespace Uchat.Server.DTOs;

public class RegisterDto
{
    [Required(ErrorMessage = "Username is required")]
    [MinLength(2, ErrorMessage = "Minimum 3 characters")]
    [MaxLength(14, ErrorMessage = "Maximum 14 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers and underscore")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;
}
