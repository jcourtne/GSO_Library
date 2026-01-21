using Microsoft.AspNetCore.Identity;

namespace GSO_Library.Models;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsDisabled { get; set; }
}
