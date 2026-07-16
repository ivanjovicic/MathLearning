using System.Security.Claims;
using MathLearning.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;

namespace MathLearning.Api.Services;

public sealed class AuthSessionValidationService
{
    public const string SecurityStampClaimType = "security_stamp";
    private const string UserIdClaimType = "userId";

    private readonly UserManager<IdentityUser> userManager;

    public AuthSessionValidationService(UserManager<IdentityUser> userManager)
    {
        this.userManager = userManager;
    }

    public async Task<string> GetCurrentSecurityStampAsync(IdentityUser user)
    {
        return await userManager.GetSecurityStampAsync(user) ?? string.Empty;
    }

    public async Task<bool> IsAccessTokenCurrentAsync(ClaimsPrincipal principal)
    {
        if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }

        var userId = principal.FindFirstValue(UserIdClaimType)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return false;
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return false;
        }

        var principalStamp = principal.FindFirstValue(SecurityStampClaimType);
        if (string.IsNullOrWhiteSpace(principalStamp))
        {
            return false;
        }

        var userStamp = await userManager.GetSecurityStampAsync(user);
        return string.Equals(principalStamp, userStamp, StringComparison.Ordinal);
    }

    public async Task<bool> IsRefreshTokenCurrentAsync(RefreshToken refreshToken)
    {
        if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(refreshToken.SecurityStamp))
        {
            return false;
        }

        var user = await userManager.FindByIdAsync(refreshToken.UserId);
        if (user is null)
        {
            return false;
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return false;
        }

        var userStamp = await userManager.GetSecurityStampAsync(user);
        return string.Equals(refreshToken.SecurityStamp, userStamp, StringComparison.Ordinal);
    }

    public async Task<bool> InvalidateUserSessionsAsync(IdentityUser user)
    {
        var result = await userManager.UpdateSecurityStampAsync(user);
        return result.Succeeded;
    }
}
