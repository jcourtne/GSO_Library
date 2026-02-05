using System.Security.Claims;
using System.Security.Cryptography;
using GSO_Library.Data;
using GSO_Library.Dtos;
using GSO_Library.Models;
using GSO_Library.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ITokenService _tokenService;
    private readonly GSOLibraryContext _context;
    private readonly IAuditService _auditService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        ITokenService tokenService,
        GSOLibraryContext context,
        IAuditService auditService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
        _context = context;
        _auditService = auditService;
    }

    [HttpGet("users")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<UserResponse>>> GetAllUsers()
    {
        var users = await _userManager.Users.ToListAsync();
        var userResponses = new List<UserResponse>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userResponses.Add(new UserResponse
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsDisabled = user.IsDisabled,
                Roles = roles.ToList()
            });
        }

        return Ok(userResponses);
    }

    [HttpGet("users/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserResponse>> GetUserById(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new UserResponse
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsDisabled = user.IsDisabled,
            Roles = roles.ToList()
        });
    }

    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            });
        }

        // Add user to User role by default
        await _userManager.AddToRoleAsync(user, "User");

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "User registered successfully",
            UserId = user.Id,
            Username = user.UserName
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var user = await _userManager.FindByNameAsync(request.Username);
        if (user == null)
        {
            await _auditService.LogAsync(AuditEventType.LoginFailure, request.Username, null, ip, "reason: unknown_user");
            return Unauthorized(new AuthResponse
            {
                Success = false,
                Message = "Invalid username or password"
            });
        }

        if (user.IsDisabled)
        {
            await _auditService.LogAsync(AuditEventType.LoginFailure, request.Username, null, ip, "reason: disabled_account");
            return Unauthorized(new AuthResponse
            {
                Success = false,
                Message = "This account has been disabled"
            });
        }

        var result = await _signInManager.PasswordSignInAsync(user, request.Password, false, false);
        if (!result.Succeeded)
        {
            await _auditService.LogAsync(AuditEventType.LoginFailure, request.Username, null, ip, "reason: wrong_password");
            return Unauthorized(new AuthResponse
            {
                Success = false,
                Message = "Invalid username or password"
            });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenService.GenerateToken(user, roles);

        var refreshToken = new RefreshToken
        {
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            UserId = user.Id
        };
        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(AuditEventType.LoginSuccess, user.UserName, null, ip, null);

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Login successful",
            Token = token,
            RefreshToken = refreshToken.Token,
            UserId = user.Id,
            Username = user.UserName
        });
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request)
    {
        var refreshToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

        if (refreshToken == null || refreshToken.IsRevoked || refreshToken.ExpiresAt <= DateTime.UtcNow)
        {
            return Unauthorized(new AuthResponse
            {
                Success = false,
                Message = "Invalid or expired refresh token"
            });
        }

        if (refreshToken.User.IsDisabled)
        {
            return Unauthorized(new AuthResponse
            {
                Success = false,
                Message = "This account has been disabled"
            });
        }

        // Revoke old token
        refreshToken.IsRevoked = true;

        // Generate new tokens
        var user = refreshToken.User;
        var roles = await _userManager.GetRolesAsync(user);
        var newJwt = _tokenService.GenerateToken(user, roles);

        var newRefreshToken = new RefreshToken
        {
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            UserId = user.Id
        };
        _context.RefreshTokens.Add(newRefreshToken);
        await _context.SaveChangesAsync();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(AuditEventType.TokenRefresh, user.UserName, null, ip, null);

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Token refreshed successfully",
            Token = newJwt,
            RefreshToken = newRefreshToken.Token,
            UserId = user.Id,
            Username = user.UserName
        });
    }

    [HttpPost("revoke-token")]
    [Authorize]
    public async Task<IActionResult> RevokeToken([FromBody] RefreshRequest request)
    {
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

        if (refreshToken == null)
            return NotFound();

        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAdmin = User.IsInRole("Admin");

        if (refreshToken.UserId != currentUserId && !isAdmin)
            return Forbid();

        refreshToken.IsRevoked = true;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("update-credentials")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> UpdateCredentials([FromBody] UpdateCredentialsRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new AuthResponse
            {
                Success = false,
                Message = "User not authenticated"
            });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new AuthResponse
            {
                Success = false,
                Message = "User not found"
            });
        }

        if (!string.IsNullOrEmpty(request.Email))
        {
            user.Email = request.Email;
        }

        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            if (string.IsNullOrEmpty(request.CurrentPassword))
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Current password is required to change password"
                });
            }

            var passwordCheckResult = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
            if (!passwordCheckResult)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Current password is incorrect"
                });
            }

            var changePasswordResult = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = string.Join(", ", changePasswordResult.Errors.Select(e => e.Description))
                });
            }
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = string.Join(", ", updateResult.Errors.Select(e => e.Description))
            });
        }

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Credentials updated successfully",
            UserId = user.Id,
            Username = user.UserName
        });
    }

    [HttpPost("disable/{userId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AuthResponse>> DisableAccount(string userId)
    {
        var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(adminId))
        {
            return Unauthorized(new AuthResponse
            {
                Success = false,
                Message = "Admin not authenticated"
            });
        }

        // Prevent admin from disabling themselves
        if (userId == adminId)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "Admin accounts cannot disable themselves"
            });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new AuthResponse
            {
                Success = false,
                Message = "User not found"
            });
        }

        user.IsDisabled = true;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            });
        }

        // Revoke all active refresh tokens for the disabled user
        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();
        foreach (var token in tokens)
            token.IsRevoked = true;
        await _context.SaveChangesAsync();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(AuditEventType.AccountDisable, User.Identity?.Name, user.UserName, ip, null);

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Account disabled successfully",
            UserId = user.Id,
            Username = user.UserName
        });
    }

    [HttpPost("enable/{userId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AuthResponse>> EnableAccount(string userId)
    {
        var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(adminId))
        {
            return Unauthorized(new AuthResponse
            {
                Success = false,
                Message = "Admin not authenticated"
            });
        }

        // Prevent admin from enabling themselves
        if (userId == adminId)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "Admin accounts cannot enable themselves"
            });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new AuthResponse
            {
                Success = false,
                Message = "User not found"
            });
        }

        user.IsDisabled = false;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(AuditEventType.AccountEnable, User.Identity?.Name, user.UserName, ip, null);

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Account enabled successfully",
            UserId = user.Id,
            Username = user.UserName
        });
    }

    [HttpPost("grant-role")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RoleManagementResponse>> GrantRole([FromBody] RoleManagementRequest request)
    {
        var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Prevent admin from modifying their own permissions
        if (request.UserId == adminId)
        {
            return BadRequest(new RoleManagementResponse
            {
                Success = false,
                Message = "Admin accounts cannot modify their own permissions"
            });
        }

        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            return NotFound(new RoleManagementResponse
            {
                Success = false,
                Message = "User not found"
            });
        }

        // Verify the role exists
        if (!await _roleManager.RoleExistsAsync(request.Role))
        {
            return BadRequest(new RoleManagementResponse
            {
                Success = false,
                Message = $"Role '{request.Role}' does not exist"
            });
        }

        // Check if user already has the role
        if (await _userManager.IsInRoleAsync(user, request.Role))
        {
            return BadRequest(new RoleManagementResponse
            {
                Success = false,
                Message = $"User already has the '{request.Role}' role"
            });
        }

        var result = await _userManager.AddToRoleAsync(user, request.Role);
        if (!result.Succeeded)
        {
            return BadRequest(new RoleManagementResponse
            {
                Success = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(AuditEventType.RoleGrant, User.Identity?.Name, user.UserName, ip, $"role: {request.Role}");

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new RoleManagementResponse
        {
            Success = true,
            Message = $"Role '{request.Role}' granted successfully",
            UserId = user.Id,
            Username = user.UserName,
            Roles = roles.ToList()
        });
    }

    [HttpPost("remove-role")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RoleManagementResponse>> RemoveRole([FromBody] RoleManagementRequest request)
    {
        var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Prevent admin from modifying their own permissions
        if (request.UserId == adminId)
        {
            return BadRequest(new RoleManagementResponse
            {
                Success = false,
                Message = "Admin accounts cannot modify their own permissions"
            });
        }

        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            return NotFound(new RoleManagementResponse
            {
                Success = false,
                Message = "User not found"
            });
        }

        // Verify the role exists
        if (!await _roleManager.RoleExistsAsync(request.Role))
        {
            return BadRequest(new RoleManagementResponse
            {
                Success = false,
                Message = $"Role '{request.Role}' does not exist"
            });
        }

        // Check if user has the role
        if (!await _userManager.IsInRoleAsync(user, request.Role))
        {
            return BadRequest(new RoleManagementResponse
            {
                Success = false,
                Message = $"User does not have the '{request.Role}' role"
            });
        }

        var result = await _userManager.RemoveFromRoleAsync(user, request.Role);
        if (!result.Succeeded)
        {
            return BadRequest(new RoleManagementResponse
            {
                Success = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(AuditEventType.RoleRemove, User.Identity?.Name, user.UserName, ip, $"role: {request.Role}");

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new RoleManagementResponse
        {
            Success = true,
            Message = $"Role '{request.Role}' removed successfully",
            UserId = user.Id,
            Username = user.UserName,
            Roles = roles.ToList()
        });
    }
}
