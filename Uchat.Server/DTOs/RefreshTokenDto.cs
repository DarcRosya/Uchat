using System.ComponentModel.DataAnnotations;

namespace Uchat.Server.DTOs;

public class RefreshTokenDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}