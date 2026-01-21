namespace GSO_Library.Dtos;

public class UpdateCredentialsRequest
{
    public string? Email { get; set; }
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
}
