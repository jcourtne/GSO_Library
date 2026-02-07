using System.ComponentModel.DataAnnotations;

namespace GSO_Library.Dtos;

public class ResetPasswordRequest
{
    [Required]
    public string NewPassword { get; set; } = string.Empty;
}
