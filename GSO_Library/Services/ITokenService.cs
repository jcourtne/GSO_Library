using GSO_Library.Models;

namespace GSO_Library.Services;

public interface ITokenService
{
    string GenerateToken(ApplicationUser user, IList<string> roles);
}
