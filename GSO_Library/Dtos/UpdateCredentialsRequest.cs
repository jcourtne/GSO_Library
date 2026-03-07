using System.ComponentModel.DataAnnotations;

namespace GSO_Library.Dtos;

public class UpdateCredentialsRequest
{
    [EmailAddress]
    [StringLength(256)]
    public string? Email { get; set; }

    [StringLength(100)]
    public string? CurrentPassword { get; set; }

    [StringLength(100, MinimumLength = 8)]
    public string? NewPassword { get; set; }
}
