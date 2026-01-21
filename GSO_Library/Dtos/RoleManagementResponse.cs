namespace GSO_Library.Dtos;

public class RoleManagementResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public List<string>? Roles { get; set; }
}
