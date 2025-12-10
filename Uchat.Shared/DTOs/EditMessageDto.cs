using System.ComponentModel.DataAnnotations;

namespace Uchat.Shared.DTOs;

public sealed class EditMessageDto
{
    [Required]
    [StringLength(1500, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}
