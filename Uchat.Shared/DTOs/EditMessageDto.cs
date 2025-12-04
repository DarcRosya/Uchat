using System.ComponentModel.DataAnnotations;

namespace Uchat.Shared.DTOs;

/// <summary>
/// DTO для редактирования сообщения
/// </summary>
public sealed class EditMessageDto
{
    [Required]
    [StringLength(5000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}
