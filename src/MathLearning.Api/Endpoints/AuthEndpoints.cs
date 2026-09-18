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
using Microsoft.Extensions.Options;

namespace MathLearning.Api.Endpoints;

public static class AuthEndpoints
{
    private const int MaxAuthUsernameLength = 128;
    private const int MaxAuthEmailLength = 254;
    private const int MaxPasswordLength = 256;
    private static readonly TimeSpan LoginRateLimitWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RegisterRateLimitWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PasswordResetRateLimitWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshRateLimitWindow = TimeSpan.FromMinutes(10);
    private const string PasswordResetRequestedMessage = "If an account matches that email, password reset instructions will be sent.";

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
            ILookupNormalizer lookupNormalizer,
            IAccountProvisioningService accountProvisioning) =>
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
                    MaxAuthUsernameLength);
                var normalizedEmail = NormalizeAuthDimension(
                    lookupNormalizer.NormalizeEmail(request.Email ?? string.Empty) ?? (request.Email ?? string.Empty),
                    MaxAuthEmailLength);

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
                    return CreateAuthRateLimitedResponse(ctx, registerRetryAfter, "registration_rate_limited");
                }

                if (canonicalUsernameForLimit.Length < 3)
                {
                    return RejectRegistration(400, "username_format", "invalid_username",
                        "Username must be at least 3 characters long");
                }

                if (canonicalUsernameForLimit.Length > MaxAuthUsernameLength)
                {
                    return RejectRegistration(400, "username_length", "invalid_username");
                }

                if (!IsValidEmailAddress(request.Email ?? string.Empty, out var canonicalEmail))
                {
                    return RejectRegistration(400, "email_format", "invalid_email");
                }

                if (!IsPasswordLengthAcceptable(request.Password))
                {
                    return RejectRegistration(400, "password_length", "invalid_password");
                }

                var canonicalUsername = canonicalUsernameForLimit;

                tx = await EconomyEndpointHelpers.BeginDbTransactionIfSupportedAsync(db, ctx.RequestAborted);

                var provisioned = await accountProvisioning.CreateCompleteAccountAsync(
                    canonicalUsername,
                    canonicalEmail,
                    request.Password,
                    request.DisplayName,
                    ctx.RequestAborted);

                if (provisioned.Conflict)
                {
                    if (tx != null)
                        await tx.RollbackAsync(ctx.RequestAborted);

                    return RejectRegistration(409, "account_conflict", "registration_conflict");
                }

                if (!provisioned.Succeeded || provisioned.User is null || provisioned.Profile is null)
                {
                    if (tx != null)
                        await tx.RollbackAsync(ctx.RequestAborted);

                    return RejectRegistration(
                        400,
                        provisioned.ValidationFailed ? "password_policy" : "identity_validation",
                        provisioned.ValidationFailed ? "invalid_password" : "registration_invalid");
                }

                user = provisioned.User;
                profile = provisioned.Profile;
                string userId = user.Id;

                // Generate tokens only after mandatory Identity + profile state is durable.
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
            catch (Exception ex) when (ex is DbException or DbUpdateException)
            {
                logger.LogError(
                    "Mobile registration failed. Reason={Reason} ExceptionType={ExceptionType} CorrelationId={CorrelationId} TraceId={TraceId}",
                    "registration_db_failure",
                    ex.GetType().Name,
                    ResolveCorrelationId(),
                    ctx.TraceIdentifier);
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
                    "Mobile registration failed. Reason={Reason} ExceptionType={ExceptionType} CorrelationId={CorrelationId} TraceId={TraceId}",
                    "registration_unexpected",
                    ex.GetType().Name,
                    ResolveCorrelationId(),
                    ctx.TraceIdentifier);
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
            LoginRequest? request,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ApiDbContext db,
            IConfiguration config,
            HttpContext ctx,
            ILogger<Program> logger,
            IRateLimitCounterStore authThrottleStore,
            ILookupNormalizer lookupNormalizer,
            IAccountProvisioningService accountProvisioning)
        {
            try
            {
                if (request is null || string.IsNullOrWhiteSpace(request.Username) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    return CreateAuthFailureResponse(
                        ctx,
                        StatusCodes.Status400BadRequest,
                        "invalid_request",
                        "A username and password are required.");
                }

                var canonicalUsername = request.Username.Trim();
                if (canonicalUsername.Length > MaxAuthUsernameLength)
                {
                    return CreateAuthFailureResponse(
                        ctx,
                        StatusCodes.Status400BadRequest,
                        "invalid_request",
                        "The username or password is invalid.");
                }

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
                    return CreateAuthRateLimitedResponse(ctx, loginRetryAfter, "login_rate_limited");
                }

                if (!IsPasswordLengthAcceptable(request.Password))
                {
                    return CreateAuthFailureResponse(
                        ctx,
                        StatusCodes.Status400BadRequest,
                        "invalid_request",
                        "The username or password is invalid.");
                }

                logger.LogInformation("Login attempt.");

                var user = await userManager.FindByNameAsync(canonicalUsername);
                if (user == null)
                {
                    logger.LogWarning("Login failed - unknown account.");
                    return CreateAuthFailureResponse(
                        ctx,
                        StatusCodes.Status401Unauthorized,
                        "invalid_credentials",
                        "Invalid username or password");
                }

                var signInResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
                if (!signInResult.Succeeded)
                {
                    logger.LogWarning(
                        "Login failed - {Reason}.",
                        signInResult.IsLockedOut ? "locked out" : "invalid password or not allowed");
                    if (signInResult.IsLockedOut)
                    {
                        var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
                        var retryAfter = lockoutEnd is null
                            ? 1
                            : (int)Math.Ceiling((lockoutEnd.Value - DateTimeOffset.UtcNow).TotalSeconds);
                        return CreateAuthRateLimitedResponse(ctx, Math.Max(1, retryAfter), "login_rate_limited");
                    }

                    return CreateAuthFailureResponse(
                        ctx,
                        StatusCodes.Status401Unauthorized,
                        "invalid_credentials",
                        "Invalid username or password");
                }

                // Identity key is the stable user id
                string userId = user.Id;

                if (!await accountProvisioning.HasCompleteProfileAsync(userId, ctx.RequestAborted))
                {
                    logger.LogWarning(
                        "Login denied - incomplete account. CorrelationId={CorrelationId}",
                        SafeClientErrorResponse.ResolveCorrelationId(ctx));
                    return CreateAuthFailureResponse(
                        ctx,
                        StatusCodes.Status403Forbidden,
                        "account_incomplete",
                        "Account setup is incomplete.");
                }

                var profile = await db.UserProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile != null)
                {
                    var today = DateOnly.FromDateTime(DateTime.UtcNow);
                    var roll = StreakRoller.Apply(profile, today);
                    if (roll != null)
                        await db.SaveChangesAsync();
                }

                logger.LogInformation("User authenticated successfully.");

                // Generate Access Token (short-lived: 30 min)
                var securityStamp = await GetCurrentSecurityStampAsync(userManager, user);
                var accessToken = await GenerateJwtTokenAsync(user, userManager, config, securityStamp, expiryMinutes: 30);

                // Generate Refresh Token (long-lived: 14 days)
                var device = NormalizeAuthDimension(ctx.Request.Headers.UserAgent.ToString(), 128);
                var ipAddress = NormalizeAuthDimension(GetPhysicalClientIp(ctx), 64);
                var refreshToken = RefreshTokenService.CreateRefreshToken(userId, securityStamp, device, ipAddress, expiryDays: 14);

                db.RefreshTokens.Add(refreshToken);
                await db.SaveChangesAsync();

                logger.LogInformation("Login successful.");

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
                return SafeClientErrorResponse.AuthUnavailableFailure(
                    ctx,
                    logger,
                    ex,
                    "Login error");
            }
        }

        group.MapPost("/login", LoginHandler).WithName("Login");
        app.MapPost("/api/auth/login", LoginHandler)
           .AllowAnonymous()
           .WithTags("Authentication")
           .WithName("LoginApiAlias");

        group.MapPost("/password/forgot", async (
            PasswordResetForgotRequest? request,
            UserManager<IdentityUser> userManager,
            IPasswordResetDelivery delivery,
            IOptions<PasswordResetDeliveryOptions> deliveryOptions,
            HttpContext ctx,
            ILogger<Program> logger,
            IRateLimitCounterStore authThrottleStore,
            ILookupNormalizer lookupNormalizer) =>
        {
            try
            {
                if (request is null || !IsValidEmailAddress(request.Email, out var canonicalEmail))
                {
                    return CreateAuthFailureResponse(
                        ctx,
                        StatusCodes.Status400BadRequest,
                        "invalid_request",
                        "A valid email address is required.");
                }

                var normalizedEmail = NormalizeAuthDimension(
                    lookupNormalizer.NormalizeEmail(canonicalEmail) ?? canonicalEmail,
                    MaxAuthEmailLength);

                if (!TryApplyAuthRateLimit(
                        authThrottleStore,
                        purpose: "password-forgot",
                        principal: normalizedEmail,
                        ctx,
                        accountLimit: 3,
                        networkLimit: 8,
                        PasswordResetRateLimitWindow,
                        out var retryAfter))
                {
                    return CreateAuthRateLimitedResponse(ctx, retryAfter, "password_reset_rate_limited");
                }

                return await ProcessForgotPasswordAsync(
                    canonicalEmail,
                    userManager,
                    delivery,
                    deliveryOptions.Value,
                    ctx,
                    logger);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Forgot-password processing failed. ExceptionType={ExceptionType} CorrelationId={CorrelationId} TraceId={TraceId}",
                    ex.GetType().Name,
                    SafeClientErrorResponse.ResolveCorrelationId(ctx),
                    SafeClientErrorResponse.ResolveTraceId(ctx));

                // Keep account existence and delivery failures indistinguishable from a missing account.
                return Results.Json(
                    new PasswordResetResponse(true, "password_reset_requested", PasswordResetRequestedMessage),
                    statusCode: StatusCodes.Status202Accepted);
            }
        }).WithName("ForgotPassword");

        group.MapPost("/password/reset", async (
            PasswordResetRequest? request,
            UserManager<IdentityUser> userManager,
            IOptions<IdentityOptions> identityOptions,
            AuthSessionValidationService sessionValidation,
            ApiDbContext db,
            HttpContext ctx,
            ILogger<Program> logger,
            IRateLimitCounterStore authThrottleStore,
            ILookupNormalizer lookupNormalizer) =>
        {
            try
            {
                if (request is null || !IsValidEmailAddress(request.Email, out var canonicalEmail) ||
                    string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 4096 ||
                    string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length > MaxPasswordLength)
                {
                    return CreatePasswordResetFailure(ctx, "password_reset_invalid", StatusCodes.Status400BadRequest);
                }

                if (request.NewPassword.Length < identityOptions.Value.Password.RequiredLength)
                {
                    return CreatePasswordResetFailure(ctx, "invalid_password", StatusCodes.Status400BadRequest);
                }

                var normalizedEmail = NormalizeAuthDimension(
                    lookupNormalizer.NormalizeEmail(canonicalEmail) ?? canonicalEmail,
                    MaxAuthEmailLength);

                if (!TryApplyAuthRateLimit(
                        authThrottleStore,
                        purpose: "password-reset",
                        principal: normalizedEmail,
                        ctx,
                        accountLimit: 5,
                        networkLimit: 12,
                        PasswordResetRateLimitWindow,
                        out var retryAfter))
                {
                    return CreateAuthRateLimitedResponse(ctx, retryAfter, "password_reset_rate_limited");
                }

                var user = await userManager.FindByEmailAsync(canonicalEmail);
                if (user is null)
                    return CreatePasswordResetFailure(ctx, "password_reset_invalid", StatusCodes.Status400BadRequest);

                var resetToken = Uri.UnescapeDataString(request.Token);
                var resetResult = await userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
                if (!resetResult.Succeeded)
                {
                    var invalidPassword = resetResult.Errors.Any(error =>
                        error.Code.StartsWith("Password", StringComparison.Ordinal));
                    return CreatePasswordResetFailure(
                        ctx,
                        invalidPassword ? "invalid_password" : "password_reset_invalid",
                        StatusCodes.Status400BadRequest);
                }

                var tx = await EconomyEndpointHelpers.BeginDbTransactionIfSupportedAsync(db, ctx.RequestAborted);
                try
                {
                    if (!await sessionValidation.InvalidateUserSessionsAsync(user))
                    {
                        if (tx != null)
                            await tx.RollbackAsync(ctx.RequestAborted);
                        return CreatePasswordResetFailure(ctx, "password_reset_unavailable", StatusCodes.Status503ServiceUnavailable);
                    }

                    var refreshTokens = await db.RefreshTokens
                        .Where(token => token.UserId == user.Id && token.RevokedAt == null)
                        .ToListAsync(ctx.RequestAborted);
                    foreach (var refreshToken in refreshTokens)
                        RefreshTokenService.RevokeToken(refreshToken);

                    await db.SaveChangesAsync(ctx.RequestAborted);
                    if (tx != null)
                        await tx.CommitAsync(ctx.RequestAborted);

                    return Results.Ok(new PasswordResetResponse(true, "password_reset_success", "Password reset successfully."));
                }
                finally
                {
                    if (tx != null)
                        await tx.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Password reset failed. ExceptionType={ExceptionType} CorrelationId={CorrelationId} TraceId={TraceId}",
                    ex.GetType().Name,
                    SafeClientErrorResponse.ResolveCorrelationId(ctx),
                    SafeClientErrorResponse.ResolveTraceId(ctx));
                return CreatePasswordResetFailure(ctx, "password_reset_unavailable", StatusCodes.Status503ServiceUnavailable);
            }
        }).WithName("ResetPassword");

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

        // REGISTER (legacy alias — same provisioning owner as mobile; tokens only after complete account)
        group.MapPost("/register", async (
            RegisterRequest request,
            UserManager<IdentityUser> userManager,
            ApiDbContext db,
            IConfiguration config,
            HttpContext ctx,
            ILogger<Program> logger,
            IRateLimitCounterStore authThrottleStore,
            ILookupNormalizer lookupNormalizer,
            IAccountProvisioningService accountProvisioning,
            IServiceScopeFactory scopeFactory) =>
        {
            IDbContextTransaction? tx = null;
            IdentityUser? user = null;
            UserProfile? profile = null;
            RefreshToken? refreshToken = null;

            try
            {
                var canonicalUsername = request.Username.Trim();
                var normalizedUsername = NormalizeAuthDimension(
                    lookupNormalizer.NormalizeName(canonicalUsername) ?? canonicalUsername,
                    MaxAuthUsernameLength);
                var normalizedEmail = NormalizeAuthDimension(
                    lookupNormalizer.NormalizeEmail(request.Email) ?? request.Email,
                    MaxAuthEmailLength);

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
                    return CreateAuthRateLimitedResponse(ctx, registerRetryAfter, "registration_rate_limited");
                }

                if (!IsValidEmailAddress(request.Email, out var canonicalEmail))
                {
                    return CreateAuthFailureResponse(ctx, StatusCodes.Status400BadRequest, "invalid_email", "Registration could not be completed.");
                }

                if (canonicalUsername.Length < 3 || canonicalUsername.Length > MaxAuthUsernameLength)
                {
                    return CreateAuthFailureResponse(ctx, StatusCodes.Status400BadRequest, "invalid_username", "Registration could not be completed.");
                }

                if (!IsPasswordLengthAcceptable(request.Password))
                {
                    return CreateAuthFailureResponse(ctx, StatusCodes.Status400BadRequest, "invalid_password", "Registration could not be completed.");
                }

                tx = await EconomyEndpointHelpers.BeginDbTransactionIfSupportedAsync(db, ctx.RequestAborted);

                var provisioned = await accountProvisioning.CreateCompleteAccountAsync(
                    canonicalUsername,
                    canonicalEmail,
                    request.Password,
                    displayName: null,
                    ctx.RequestAborted);

                if (provisioned.Conflict)
                {
                    if (tx != null)
                        await tx.RollbackAsync(ctx.RequestAborted);

                    return CreateAuthFailureResponse(ctx, StatusCodes.Status409Conflict, "registration_conflict", "Registration could not be completed.");
                }

                if (!provisioned.Succeeded || provisioned.User is null || provisioned.Profile is null)
                {
                    if (tx != null)
                        await tx.RollbackAsync(ctx.RequestAborted);

                    return CreateAuthFailureResponse(ctx, StatusCodes.Status400BadRequest, "registration_invalid", "Registration could not be completed.");
                }

                user = provisioned.User;
                profile = provisioned.Profile;
                string userId = user.Id;

                var securityStamp = await GetCurrentSecurityStampAsync(userManager, user);
                var accessToken = await GenerateJwtTokenAsync(user, userManager, config, securityStamp, expiryMinutes: 30);

                var device = NormalizeAuthDimension(ctx.Request.Headers.UserAgent.ToString(), 128);
                var ipAddress = NormalizeAuthDimension(GetPhysicalClientIp(ctx), 64);
                refreshToken = RefreshTokenService.CreateRefreshToken(userId, securityStamp, device, ipAddress, expiryDays: 14);

                db.RefreshTokens.Add(refreshToken);
                await db.SaveChangesAsync();

                if (tx != null)
                    await tx.CommitAsync(ctx.RequestAborted);

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
                if (tx != null)
                {
                    try
                    {
                        await tx.RollbackAsync(ctx.RequestAborted);
                    }
                    catch (Exception rollbackEx)
                    {
                        logger.LogWarning(rollbackEx, "Rollback after legacy registration failure failed.");
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

                logger.LogError(
                    "Register error. ExceptionType={ExceptionType} CorrelationId={CorrelationId} TraceId={TraceId}",
                    ex.GetType().Name,
                    SafeClientErrorResponse.ResolveCorrelationId(ctx),
                    SafeClientErrorResponse.ResolveTraceId(ctx));
                return CreateAuthFailureResponse(
                    ctx,
                    StatusCodes.Status503ServiceUnavailable,
                    "registration_unavailable",
                    "Registration is temporarily unavailable. Please try again later.");
            }
            finally
            {
                if (tx != null)
                    await tx.DisposeAsync();
            }
        });

        // TEST endpoint (no auth required)
        group.MapGet("/test", () => Results.Ok(new
        {
            message = "Auth endpoints are working!",
            timestamp = DateTime.UtcNow
        })).WithName("TestAuth");
    }

    private static async Task<IResult> ProcessForgotPasswordAsync(
        string canonicalEmail,
        UserManager<IdentityUser> userManager,
        IPasswordResetDelivery delivery,
        PasswordResetDeliveryOptions deliveryOptions,
        HttpContext ctx,
        ILogger<Program> logger)
    {
        var user = await userManager.FindByEmailAsync(canonicalEmail);
        if (user is null)
        {
            // Deliberately perform no account-specific work for an unknown address.
            return Results.Json(
                new PasswordResetResponse(true, "password_reset_requested", PasswordResetRequestedMessage),
                statusCode: StatusCodes.Status202Accepted);
        }

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var separator = deliveryOptions.ResetBaseUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var resetUrl = $"{deliveryOptions.ResetBaseUrl}{separator}email={Uri.EscapeDataString(canonicalEmail)}&token={Uri.EscapeDataString(resetToken)}";
        var delivered = await delivery.SendAsync(
            new PasswordResetDeliveryMessage(canonicalEmail, resetToken, resetUrl),
            ctx.RequestAborted);

        if (!delivered)
        {
            logger.LogWarning(
                "Password-reset delivery unavailable. Reason={Reason} CorrelationId={CorrelationId} TraceId={TraceId}",
                "delivery_unavailable",
                SafeClientErrorResponse.ResolveCorrelationId(ctx),
                SafeClientErrorResponse.ResolveTraceId(ctx));
        }

        return Results.Json(
            new PasswordResetResponse(true, "password_reset_requested", PasswordResetRequestedMessage),
            statusCode: StatusCodes.Status202Accepted);
    }

    private static IResult CreateAuthFailureResponse(
        HttpContext ctx,
        int statusCode,
        string code,
        string message,
        int? retryAfterSeconds = null) =>
        Results.Json(
            new AuthFailureResponse(
                code,
                message,
                SafeClientErrorResponse.ResolveCorrelationId(ctx),
                retryAfterSeconds),
            statusCode: statusCode);

    private static IResult CreatePasswordResetFailure(HttpContext ctx, string code, int statusCode)
    {
        var message = code switch
        {
            "invalid_password" => "The new password does not meet the password requirements.",
            "password_reset_rate_limited" => "Too many password reset attempts. Try again later.",
            "password_reset_unavailable" => "Password reset is temporarily unavailable. Please try again later.",
            _ => "The password reset request is invalid."
        };

        return CreateAuthFailureResponse(ctx, statusCode, code, message);
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

    private static IResult CreateAuthRateLimitedResponse(
        HttpContext ctx,
        int retryAfterSeconds,
        string code = "login_rate_limited")
    {
        var boundedRetryAfter = Math.Max(1, retryAfterSeconds);
        ctx.Response.Headers["Retry-After"] = boundedRetryAfter.ToString(CultureInfo.InvariantCulture);
        return CreateAuthFailureResponse(
            ctx,
            StatusCodes.Status429TooManyRequests,
            code,
            "Too many attempts. Try again later.",
            boundedRetryAfter);
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
            return canonicalEmail.Length <= MaxAuthEmailLength;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsPasswordLengthAcceptable(string password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length <= MaxPasswordLength;

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
