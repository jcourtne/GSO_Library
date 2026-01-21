namespace GSO_Library.Dtos;

public class AuthResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Token { get; set; }
    public string? UserId { get; set; }
    public string? Username { get; set; }
}
