using MathLearning.Application.DTOs.Auth;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Api.Services;
using MathLearning.Api.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.IdentityModel.Tokens;
using System.Data.Common;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Api.Endpoints;

public static class AuthEndpoints
{
    private static readonly TimeSpan LoginRateLimitWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RegisterRateLimitWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshRateLimitWindow = TimeSpan.FromMinutes(10);

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
                       .AllowAnonymous()
                       .WithTags("Authentication");

        static async Task CleanupMobileRegistrationFailureAsync(
            IServiceScopeFactory scopeFactory,
            IdentityUser? user,
            UserProfile? profile,
            RefreshToken? refreshToken,
            ILogger<Program> logger,
            CancellationToken cancellationToken)
        {
            await using var cleanupScope = scopeFactory.CreateAsyncScope();
            var cleanupDb = cleanupScope.ServiceProvider.GetRequiredService<ApiDbContext>();

            try
            {
                if (user != null)
                {
                    var persistedUser = await cleanupDb.Users.SingleOrDefaultAsync(u => u.Id == user.Id, cancellationToken);
                    if (persistedUser != null)
                        cleanupDb.Users.Remove(persistedUser);
                }

                if (refreshToken != null)
                {
                    var persistedRefreshToken = await cleanupDb.RefreshTokens
                        .SingleOrDefaultAsync(t => t.Token == refreshToken.Token, cancellationToken);
                    if (persistedRefreshToken != null)
                        cleanupDb.RefreshTokens.Remove(persistedRefreshToken);
                }

                if (profile != null)
                {
                    var persistedProfile = await cleanupDb.UserProfiles
                        .SingleOrDefaultAsync(p => p.UserId == profile.UserId, cancellationToken);
                    if (persistedProfile != null)
                        cleanupDb.UserProfiles.Remove(persistedProfile);
                }

                if (user != null || refreshToken != null || profile != null)
                    await cleanupDb.SaveChangesAsync(cancellationToken);
            }
            catch (Exception cleanupEx)
            {
                logger.LogWarning(
                    "Cleanup after mobile registration failure failed. ExceptionType={ExceptionType}",
                    cleanupEx.GetType().Name);
            }
        }

        // 📱 MOBILE REGISTRATION (Public)
        group.MapPost("/mobile/register", async (
            MobileRegisterRequest request,
            UserManager<IdentityUser> userManager,
            ApiDbContext db,
            IConfiguration config,
            HttpContext ctx,
            ILogger<Program> logger,
            IServiceScopeFactory scopeFactory,
            IRateLimitCounterStore authThrottleStore,
            ILookupNormalizer lookupNormalizer) =>
        {
            IDbContextTransaction? tx = null;
            IdentityUser? user = null;
            UserProfile? profile = null;
            RefreshToken? refreshToken = null;

            string ResolveCorrelationId() =>
                SafeClientErrorResponse.ResolveCorrelationId(ctx)
                ?? ctx.TraceIdentifier;

            IResult RejectRegistration(int statusCode, string diagnosticReason, string publicCode,
                string message = "Registration could not be completed")
            {
                logger.LogWarning(
                    "Mobile registration rejected. Reason={Reason} StatusCode={StatusCode} CorrelationId={CorrelationId} TraceId={TraceId}",
                    diagnosticReason, statusCode, ResolveCorrelationId(), ctx.TraceIdentifier);
                return Results.Json(
                    new MobileRegisterResponse(Success: false, Message: message, Code: publicCode),
                    statusCode: statusCode);
            }

            try
            {
                var canonicalUsernameForLimit = (request.Username ?? string.Empty).Trim();
                var normalizedUsername = NormalizeAuthDimension(
                    lookupNormalizer.NormalizeName(canonicalUsernameForLimit) ?? canonicalUsernameForLimit,
                    128);
                var normalizedEmail = NormalizeAuthDimension(
                    lookupNormalizer.NormalizeEmail(request.Email ?? string.Empty) ?? (request.Email ?? string.Empty),
                    256);

                if (!TryApplyAuthRateLimit(
                        authThrottleStore,
                        purpose: "mobile-register",
                        principal: $"{normalizedUsername}:{normalizedEmail}",
                        ctx,
                        accountLimit: 3,
                        networkLimit: 9,
                        RegisterRateLimitWindow,
                        out var registerRetryAfter))
                {
                    logger.LogWarning(
                        "Mobile registration rejected. Reason={Reason} StatusCode={StatusCode} CorrelationId={CorrelationId} TraceId={TraceId}",
                        "registration_rate_limited",
                        StatusCodes.Status429TooManyRequests,
                        ResolveCorrelationId(),
                        ctx.TraceIdentifier);
                    return CreateAuthRateLimitedResponse(ctx, registerRetryAfter);
                }

                if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
                {
                    return RejectRegistration(400, "username_format", "invalid_username",
                        "Username must be at least 3 characters long");
                }

                if (!IsValidEmailAddress(request.Email ?? string.Empty, out var canonicalEmail))
                {
                    return RejectRegistration(400, "email_format", "invalid_email");
                }

                if (!IsRegistrationPasswordLengthAcceptable(request.Password))
                {
                    return RejectRegistration(400, "password_length", "invalid_password");
                }

                var canonicalUsername = request.Username.Trim();

                var existingUser = await userManager.FindByNameAsync(canonicalUsername);
                if (existingUser != null)
                {
                    return RejectRegistration(409, "account_conflict", "registration_conflict");
                }

                var existingEmail = await userManager.FindByEmailAsync(canonicalEmail);
                if (existingEmail != null)
                {
                    return RejectRegistration(409, "account_conflict", "registration_conflict");
                }

                tx = await EconomyEndpointHelpers.BeginDbTransactionIfSupportedAsync(db, ctx.RequestAborted);

                // Create Identity user
                user = new IdentityUser
                {
                    UserName = canonicalUsername,
                    Email = canonicalEmail,
                    EmailConfirmed = true,
                    LockoutEnabled = true
                };

                var result = await userManager.CreateAsync(user, request.Password);

                if (!result.Succeeded)
                {
                    if (tx != null)
                        await tx.RollbackAsync(ctx.RequestAborted);

                    var passwordRejected = result.Errors.Any(error => error.Code is
                        "PasswordTooShort" or "PasswordRequiresUniqueChars" or "PasswordRequiresDigit" or
                        "PasswordRequiresLower" or "PasswordRequiresUpper" or "PasswordRequiresNonAlphanumeric");
                    return RejectRegistration(
                        400,
                        passwordRejected ? "password_policy" : "identity_validation",
                        passwordRejected ? "invalid_password" : "registration_invalid");
                }

                // Identity key is the stable user id
                string userId = user.Id;

                // Create UserProfile
                profile = new UserProfile
                {
                    UserId = userId,
                    Username = canonicalUsername,
                    DisplayName = request.DisplayName ?? canonicalUsername,
                    Coins = 100, // Welcome bonus
                    Level = 1,
                    Xp = 0,
                    Streak = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.UserProfiles.Add(profile);
                await db.SaveChangesAsync();

                // Generate tokens
                var securityStamp = await GetCurrentSecurityStampAsync(userManager, user);
                var accessToken = await GenerateJwtTokenAsync(user, userManager, config, securityStamp, expiryMinutes: 30);

                var device = NormalizeAuthDimension(ctx.Request.Headers.UserAgent.ToString(), 128);
                var ipAddress = NormalizeAuthDimension(GetPhysicalClientIp(ctx), 64);
                refreshToken = RefreshTokenService.CreateRefreshToken(userId, securityStamp, device, ipAddress, expiryDays: 14);

                db.RefreshTokens.Add(refreshToken);
                await db.SaveChangesAsync();

                if (tx != null)
                    await tx.CommitAsync(ctx.RequestAborted);

                return Results.Ok(new MobileRegisterResponse(
                    Success: true,
                    Message: "Registration successful",
                    Tokens: new TokenResponse(
                        AccessToken: accessToken,
                        RefreshToken: refreshToken.Token,
                        ExpiresIn: 1800,
                        UserId: userId,
                        Username: canonicalUsername
                    ),
                    Profile: new UserProfileDto(
                        UserId: userId,
                        Username: profile.Username,
                        DisplayName: profile.DisplayName,
                        Coins: profile.Coins,
                        Level: profile.Level,
                        Xp: profile.Xp,
                        Streak: profile.Streak,
                        CreatedAt: profile.CreatedAt
                    )
                ));
            }
            catch (DbException ex)
            {
                logger.LogError(
                    "Mobile registration failed. Reason={Reason} ExceptionType={ExceptionType} CorrelationId={CorrelationId} TraceId={TraceId} StackTrace={StackTrace}",
                    "registration_db_failure",
                    ex.GetType().Name,
                    ResolveCorrelationId(),
                    ctx.TraceIdentifier,
                    ex.StackTrace);
                if (tx != null)
                {
                    try
                    {
                        await tx.RollbackAsync(ctx.RequestAborted);
                    }
                    catch (Exception rollbackEx)
                    {
                        logger.LogWarning(
                            "Rollback after mobile registration failure failed. ExceptionType={ExceptionType}",
                            rollbackEx.GetType().Name);
                    }
                }
                else
                {
                    await CleanupMobileRegistrationFailureAsync(
                        scopeFactory,
                        user,
                        profile,
                        refreshToken,
                        logger,
                        ctx.RequestAborted);
                }

                return Results.Json(new MobileRegisterResponse(
                    Success: false,
                    Message: "Registration failed. Please try again.",
                    Code: "registration_unavailable"
                ), statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (Exception ex)
            {
                // Exception messages can contain database/account data. Keep only safe diagnostics.
                logger.LogError(
                    "Mobile registration failed. Reason={Reason} ExceptionType={ExceptionType} CorrelationId={CorrelationId} TraceId={TraceId} StackTrace={StackTrace}",
                    "registration_unexpected",
                    ex.GetType().Name,
                    ResolveCorrelationId(),
                    ctx.TraceIdentifier,
                    ex.StackTrace);
                if (tx != null)
                {
                    try
                    {
                        await tx.RollbackAsync(ctx.RequestAborted);
                    }
                    catch (Exception rollbackEx)
                    {
                        logger.LogWarning(
                            "Rollback after mobile registration failure failed. ExceptionType={ExceptionType}",
                            rollbackEx.GetType().Name);
                    }
                }
                else
                {
                    await CleanupMobileRegistrationFailureAsync(
                        scopeFactory,
                        user,
                        profile,
                        refreshToken,
                        logger,
                        ctx.RequestAborted);
                }

                return Results.Json(new MobileRegisterResponse(
                    Success: false,
                    Message: "Registration failed. Please try again.",
                    Code: "registration_unexpected"
                ), statusCode: 500);
            }
            finally
            {
                if (tx != null)
                    await tx.DisposeAsync();
            }
        })
        .WithName("MobileRegister")
        .WithDescription("Register new mobile user");

        // 🔐 LOGIN (sa Refresh Token)
        static async Task<IResult> LoginHandler(
            LoginRequest request,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ApiDbContext db,
            IConfiguration config,
            HttpContext ctx,
            ILogger<Program> logger,
            IRateLimitCounterStore authThrottleStore,
            ILookupNormalizer lookupNormalizer)
        {
            try
            {
                var canonicalUsername = request.Username.Trim();
                var normalizedUsername = NormalizeAuthDimension(
                    lookupNormalizer.NormalizeName(canonicalUsername) ?? canonicalUsername,
                    128);

                if (!TryApplyAuthRateLimit(
                        authThrottleStore,
                        purpose: "login",
                        principal: normalizedUsername,
                        ctx,
                        accountLimit: 5,
                        networkLimit: 15,
                        LoginRateLimitWindow,
                        out var loginRetryAfter))
                {
                    return CreateAuthRateLimitedResponse(ctx, loginRetryAfter);
                }

                if (!IsPasswordLengthAcceptable(request.Password))
                {
                    return Results.Json(new { error = "Invalid username or password" }, statusCode: 401);
                }

                logger.LogInformation("Login attempt for username: {Username}", normalizedUsername);

                var user = await userManager.FindByNameAsync(canonicalUsername);
                if (user == null)
                {
                    logger.LogWarning("Login failed - user not found: {Username}", normalizedUsername);
                    return Results.Json(new { error = "Invalid username or password" }, statusCode: 401);
                }

                var signInResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
                if (!signInResult.Succeeded)
                {
                    logger.LogWarning(
                        "Login failed - {Reason} for user: {Username}",
                        signInResult.IsLockedOut ? "locked out" : "invalid password or not allowed",
                        normalizedUsername);
                    return Results.Json(new { error = "Invalid username or password" }, statusCode: 401);
                }

                // Identity key is the stable user id
                string userId = user.Id;

                var profile = await db.UserProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile != null)
                {
                    var today = DateOnly.FromDateTime(DateTime.UtcNow);
                    var roll = StreakRoller.Apply(profile, today);
                    if (roll != null)
                        await db.SaveChangesAsync();
                }

                logger.LogInformation("User authenticated successfully: {Username}, UserId: {UserId}", canonicalUsername, userId);

                // Generate Access Token (short-lived: 30 min)
                var securityStamp = await GetCurrentSecurityStampAsync(userManager, user);
                var accessToken = await GenerateJwtTokenAsync(user, userManager, config, securityStamp, expiryMinutes: 30);

                // Generate Refresh Token (long-lived: 14 days)
                var device = NormalizeAuthDimension(ctx.Request.Headers.UserAgent.ToString(), 128);
                var ipAddress = NormalizeAuthDimension(GetPhysicalClientIp(ctx), 64);
                var refreshToken = RefreshTokenService.CreateRefreshToken(userId, securityStamp, device, ipAddress, expiryDays: 14);

                db.RefreshTokens.Add(refreshToken);
                await db.SaveChangesAsync();

                logger.LogInformation("Login successful for user: {Username}", canonicalUsername);

                return Results.Ok(new TokenResponse(
                    AccessToken: accessToken,
                    RefreshToken: refreshToken.Token,
                    ExpiresIn: 1800, // 30 minutes in seconds
                    UserId: userId,
                    Username: user.UserName ?? ""
                ));
            }
            catch (Exception ex)
            {
                return SafeClientErrorResponse.AuthUnexpectedFailure(
                    ctx,
                    logger,
                    ex,
                    "Login error for username: {Username}",
                    request.Username);
            }
        }

        group.MapPost("/login", LoginHandler).WithName("Login");
        app.MapPost("/api/auth/login", LoginHandler)
           .AllowAnonymous()
           .WithTags("Authentication")
           .WithName("LoginApiAlias");

        // 🔄 REFRESH TOKEN
        group.MapPost("/refresh", async (
            TokenRequest request,
            UserManager<IdentityUser> userManager,
            ApiDbContext db,
            IConfiguration config,
            HttpContext ctx,
            ILogger<Program> logger,
            IRateLimitCounterStore authThrottleStore) =>
        {
            try
            {
                // Find refresh token
                var refreshToken = await db.RefreshTokens
                    .FirstOrDefaultAsync(t => t.Token == request.RefreshToken);

                // Validate token
                if (!RefreshTokenService.ValidateRefreshToken(refreshToken))
                {
                    return Results.Json(new { error = "Invalid or expired refresh token" }, statusCode: 401);
                }

                // Get user
                var user = await userManager.FindByIdAsync(refreshToken!.UserId);
                if (user == null)
                {
                    return Results.Json(new { error = "Invalid or expired refresh token" }, statusCode: 401);
                }

                var securityStamp = await GetCurrentSecurityStampAsync(userManager, user);
                if (!RefreshTokenService.ValidateRefreshToken(refreshToken, securityStamp))
                {
                    return Results.Json(new { error = "Invalid or expired refresh token" }, statusCode: 401);
                }

                var device = NormalizeAuthDimension(ctx.Request.Headers.UserAgent.ToString(), 128);
                var ipAddress = NormalizeAuthDimension(GetPhysicalClientIp(ctx), 64);
                if (!TryApplyAuthRateLimit(
                        authThrottleStore,
                        purpose: "refresh",
                        principal: refreshToken.UserId,
                        ctx,
                        accountLimit: 10,
                        networkLimit: 30,
                        RefreshRateLimitWindow,
                        out var refreshRetryAfter))
                {
                    return CreateAuthRateLimitedResponse(ctx, refreshRetryAfter);
                }

                // Revoke old refresh token
                RefreshTokenService.RevokeToken(refreshToken);

                // Generate new tokens
                var newAccessToken = await GenerateJwtTokenAsync(user, userManager, config, securityStamp, expiryMinutes: 30);

                var newRefreshToken = RefreshTokenService.CreateRefreshToken(refreshToken.UserId, securityStamp, device, ipAddress, expiryDays: 14);

                db.RefreshTokens.Add(newRefreshToken);
                await db.SaveChangesAsync();

                return Results.Ok(new TokenResponse(
                    AccessToken: newAccessToken,
                    RefreshToken: newRefreshToken.Token,
                    ExpiresIn: 1800, // 30 minutes
                    UserId: refreshToken.UserId,
                    Username: user.UserName ?? ""
                ));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                logger.LogInformation(ex, "Refresh token reuse detected during concurrent rotation.");
                return Results.Json(new { error = "Invalid or expired refresh token" }, statusCode: 401);
            }
            catch (Exception ex)
            {
                return SafeClientErrorResponse.AuthUnexpectedFailure(ctx, logger, ex, "Refresh token error");
            }
        }).WithName("RefreshToken");

        // 🚪 LOGOUT (revoke refresh token)
        group.MapPost("/logout", async (
            RevokeTokenRequest request,
            ApiDbContext db,
            HttpContext ctx,
            ILogger<Program> logger) =>
        {
            try
            {
                var refreshToken = await db.RefreshTokens
                    .FirstOrDefaultAsync(t => t.Token == request.RefreshToken);

                if (refreshToken == null)
                {
                    return Results.Json(new { error = "Token not found" }, statusCode: 404);
                }

                RefreshTokenService.RevokeToken(refreshToken);
                await db.SaveChangesAsync();

                return Results.Ok(new { message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                return SafeClientErrorResponse.AuthUnexpectedFailure(ctx, logger, ex, "Logout error");
            }
        }).WithName("Logout");

        // 🔒 REVOKE ALL TOKENS (logout from all devices)
        group.MapPost("/revoke-all", async (
            UserManager<IdentityUser> userManager,
            ApiDbContext db,
            HttpContext ctx,
            ILogger<Program> logger) =>
        {
            try
            {
                string userId = ctx.User.FindFirst("userId")!.Value;
                var user = await userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return Results.Json(new { error = "User not found" }, statusCode: 401);
                }

                var tx = await EconomyEndpointHelpers.BeginDbTransactionIfSupportedAsync(db, ctx.RequestAborted);

                try
                {
                    var securityStampResult = await userManager.UpdateSecurityStampAsync(user);
                    if (!securityStampResult.Succeeded)
                    {
                        if (tx != null)
                            await tx.RollbackAsync(ctx.RequestAborted);

                        return Results.Json(new { error = "Unable to revoke tokens" }, statusCode: 500);
                    }

                    var userTokens = await db.RefreshTokens
                        .Where(t => t.UserId == userId && t.RevokedAt == null)
                        .ToListAsync();

                    foreach (var token in userTokens)
                    {
                        RefreshTokenService.RevokeToken(token);
                    }

                    await db.SaveChangesAsync();

                    if (tx != null)
                        await tx.CommitAsync(ctx.RequestAborted);

                    return Results.Ok(new
                    {
                        message = $"Revoked {userTokens.Count} tokens",
                        revokedCount = userTokens.Count
                    });
                }
                finally
                {
                    if (tx != null)
                        await tx.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                return SafeClientErrorResponse.AuthUnexpectedFailure(ctx, logger, ex, "Revoke-all tokens error");
            }
        })
        .RequireAuthorization()
        .WithName("RevokeAllTokens");

        // REGISTER (Admin - existing)
        group.MapPost("/register", async (
            RegisterRequest request,
            UserManager<IdentityUser> userManager,
            ApiDbContext db,
            IConfiguration config,
            HttpContext ctx,
            ILogger<Program> logger,
            IRateLimitCounterStore authThrottleStore,
            ILookupNormalizer lookupNormalizer) =>
        {
            try
            {
                var canonicalUsername = request.Username.Trim();
                var normalizedUsername = NormalizeAuthDimension(
                    lookupNormalizer.NormalizeName(canonicalUsername) ?? canonicalUsername,
                    128);
                var normalizedEmail = NormalizeAuthDimension(
                    lookupNormalizer.NormalizeEmail(request.Email) ?? request.Email,
                    256);

                if (!TryApplyAuthRateLimit(
                        authThrottleStore,
                        purpose: "register",
                        principal: $"{normalizedUsername}:{normalizedEmail}",
                        ctx,
                        accountLimit: 3,
                        networkLimit: 9,
                        RegisterRateLimitWindow,
                        out var registerRetryAfter))
                {
                    return CreateAuthRateLimitedResponse(ctx, registerRetryAfter);
                }

                if (!IsValidEmailAddress(request.Email, out var canonicalEmail))
                {
                    return Results.Json(new { error = "Registration could not be completed" }, statusCode: 400);
                }

                if (!IsRegistrationPasswordLengthAcceptable(request.Password))
                {
                    return Results.Json(new { error = "Registration could not be completed" }, statusCode: 400);
                }

                var existingUser = await userManager.FindByNameAsync(canonicalUsername);
                if (existingUser != null)
                {
                    return Results.Json(new { error = "Registration could not be completed" }, statusCode: 409);
                }

                var existingEmail = await userManager.FindByEmailAsync(canonicalEmail);
                if (existingEmail != null)
                {
                    return Results.Json(new { error = "Registration could not be completed" }, statusCode: 409);
                }

                var user = new IdentityUser
                {
                    UserName = canonicalUsername,
                    Email = canonicalEmail,
                    EmailConfirmed = true,
                    LockoutEnabled = true
                };

                var result = await userManager.CreateAsync(user, request.Password);

                if (!result.Succeeded)
                {
                    return Results.Json(new { error = "Registration could not be completed" }, statusCode: 400);
                }

                string userId = user.Id;

                // Generate tokens
                var securityStamp = await GetCurrentSecurityStampAsync(userManager, user);
                var accessToken = await GenerateJwtTokenAsync(user, userManager, config, securityStamp, expiryMinutes: 30);

                var device = NormalizeAuthDimension(ctx.Request.Headers.UserAgent.ToString(), 128);
                var ipAddress = NormalizeAuthDimension(GetPhysicalClientIp(ctx), 64);
                var refreshToken = RefreshTokenService.CreateRefreshToken(userId, securityStamp, device, ipAddress, expiryDays: 14);

                db.RefreshTokens.Add(refreshToken);
                await db.SaveChangesAsync();

                return Results.Ok(new TokenResponse(
                    AccessToken: accessToken,
                    RefreshToken: refreshToken.Token,
                    ExpiresIn: 1800,
                    UserId: userId,
                    Username: canonicalUsername
                ));
            }
            catch (Exception ex)
            {
                return SafeClientErrorResponse.AuthUnexpectedFailure(ctx, logger, ex, "Register error");
            }
        });

        // TEST endpoint (no auth required)
        group.MapGet("/test", () => Results.Ok(new
        {
            message = "Auth endpoints are working!",
            timestamp = DateTime.UtcNow
        })).WithName("TestAuth");
    }

    private static bool TryApplyAuthRateLimit(
        IRateLimitCounterStore authThrottleStore,
        string purpose,
        string principal,
        HttpContext ctx,
        int accountLimit,
        int networkLimit,
        TimeSpan window,
        out int retryAfterSeconds)
    {
        var safePrincipal = NormalizeAuthDimension(principal, 128);
        var ipAddress = NormalizeAuthDimension(GetPhysicalClientIp(ctx), 64);
        var device = NormalizeAuthDimension(ctx.Request.Headers.UserAgent.ToString(), 128);

        if (!authThrottleStore.TryAcquire($"{purpose}:account:{safePrincipal}", accountLimit, window, out retryAfterSeconds))
            return false;

        if (!authThrottleStore.TryAcquire($"{purpose}:network:{ipAddress}:{device}", networkLimit, window, out retryAfterSeconds))
            return false;

        return true;
    }

    private static IResult CreateAuthRateLimitedResponse(HttpContext ctx, int retryAfterSeconds)
    {
        var boundedRetryAfter = Math.Max(1, retryAfterSeconds);
        ctx.Response.Headers["Retry-After"] = boundedRetryAfter.ToString(CultureInfo.InvariantCulture);
        return Results.Json(
            new { error = "Too many attempts. Try again later." },
            statusCode: StatusCodes.Status429TooManyRequests);
    }

    private static string NormalizeAuthDimension(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        var normalized = value.Trim().Normalize(NormalizationForm.FormKC);
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string GetPhysicalClientIp(HttpContext ctx)
    {
        var physicalIp = ctx.Items[ConnectionRemoteIpMiddleware.ItemKey] as IPAddress
            ?? ctx.Connection.RemoteIpAddress;

        return physicalIp?.ToString() ?? "unknown";
    }

    private static bool IsValidEmailAddress(string email, out string canonicalEmail)
    {
        canonicalEmail = string.Empty;

        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var parsed = new MailAddress(email.Trim());
            canonicalEmail = parsed.Address;
            return canonicalEmail.Length <= 254;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsPasswordLengthAcceptable(string password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length <= 256;

    /// <summary>
    /// Registration-only password length. Login keeps [IsPasswordLengthAcceptable]
    /// so existing accounts with shorter historical passwords can still sign in.
    /// </summary>
    private static bool IsRegistrationPasswordLengthAcceptable(string password) =>
        !string.IsNullOrWhiteSpace(password)
        && password.Length >= 10
        && password.Length <= 256;

    private static Task<string> GetCurrentSecurityStampAsync(
        UserManager<IdentityUser> userManager,
        IdentityUser user) =>
        userManager.GetSecurityStampAsync(user);

    private static async Task<string> GenerateJwtTokenAsync(
        IdentityUser user,
        UserManager<IdentityUser> userManager,
        IConfiguration config,
        string securityStamp,
        int expiryMinutes = 30)
    {
        var jwtSettings = config.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
        var issuer = jwtSettings["Issuer"] ?? "MathLearningAPI";
        var audience = jwtSettings["Audience"] ?? "MathLearningApp";

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        string userId = user.Id;

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? ""),
            new Claim("userId", userId),
            new Claim(AuthSessionValidationService.SecurityStampClaimType, securityStamp),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var roles = await userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // No legacy id mapping: the app uses Identity's string key end-to-end.
}
