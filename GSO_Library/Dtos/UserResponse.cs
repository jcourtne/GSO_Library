namespace GSO_Library.Dtos;

public class UserResponse
{
    public string Id { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsDisabled { get; set; }
    public List<string> Roles { get; set; } = [];
}
