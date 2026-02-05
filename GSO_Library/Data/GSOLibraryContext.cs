using GSO_Library.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GSO_Library.Data;

public class GSOLibraryContext : IdentityDbContext<ApplicationUser>
{
    public GSOLibraryContext(DbContextOptions<GSOLibraryContext> options) : base(options)
    {
    }
}
