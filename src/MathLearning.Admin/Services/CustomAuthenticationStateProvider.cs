using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace MathLearning.Admin.Services;

public class CustomAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IdentityOptions _options;
    private readonly ILogger<CustomAuthenticationStateProvider> _logger;

    public CustomAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<CustomAuthenticationStateProvider> logger)
        : base(loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _options = optionsAccessor.Value;
        _logger = logger;
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        var user = authenticationState.User;
        var isAuthenticated = user?.Identity?.IsAuthenticated ?? false;
        
        _logger.LogDebug("ValidateAuthenticationStateAsync - User: {UserName}, IsAuth: {IsAuth}", 
            user?.Identity?.Name ?? "null", isAuthenticated);

        if (!isAuthenticated)
        {
            return false;
        }

        // Get the user from database
        await using var scope = _scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var result = await ValidateSecurityStampAsync(userManager, authenticationState.User);
        
        _logger.LogDebug("Security stamp validation executed for {UserName}", user?.Identity?.Name ?? "null");
        
        return result;
    }

    private async Task<bool> ValidateSecurityStampAsync(UserManager<IdentityUser> userManager, ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user == null)
        {
            _logger.LogWarning("User not found in database");
            return false;
        }
        else if (!userManager.SupportsUserSecurityStamp)
        {
            _logger.LogDebug("Security stamp not supported");
            return true;
        }
        else
        {
            var principalStamp = principal.FindFirstValue(_options.ClaimsIdentity.SecurityStampClaimType);
            var userStamp = await userManager.GetSecurityStampAsync(user);
            var isValid = principalStamp == userStamp;
            
            _logger.LogDebug("Security stamp validation completed for {UserName}: {IsValid}", 
                user.UserName ?? "unknown", isValid);
            
            return isValid;
        }
    }
}
