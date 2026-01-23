using MathLearning.Application.DTOs.Auth;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MathLearning.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
                       .AllowAnonymous()
                       .WithTags("Authentication");

        // 🔐 LOGIN (sa Refresh Token)
        group.MapPost("/login", async (
            LoginRequest request,
            UserManager<IdentityUser> userManager,
            ApiDbContext db,
            IConfiguration config,
            HttpContext ctx) =>
        {
            try
            {
                var user = await userManager.FindByNameAsync(request.Username);
                if (user == null)
                {
                    return Results.Json(new { error = "Invalid username or password" }, statusCode: 401);
                }

                var isPasswordValid = await userManager.CheckPasswordAsync(user, request.Password);
                if (!isPasswordValid)
                {
                    return Results.Json(new { error = "Invalid username or password" }, statusCode: 401);
                }

                // Parse userId
                int userId;
                if (!int.TryParse(user.Id, out userId))
                {
                    userId = Math.Abs(user.Id.GetHashCode());
                }

                // Generate Access Token (short-lived: 30 min)
                var accessToken = GenerateJwtToken(user, config, expiryMinutes: 30);

                // Generate Refresh Token (long-lived: 14 days)
                var device = ctx.Request.Headers.UserAgent.ToString();
                var ipAddress = ctx.Connection.RemoteIpAddress?.ToString();
                var refreshToken = RefreshTokenService.CreateRefreshToken(userId, device, ipAddress, expiryDays: 14);

                db.RefreshTokens.Add(refreshToken);
                await db.SaveChangesAsync();

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
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        }).WithName("Login");

        // 🔄 REFRESH TOKEN
        group.MapPost("/refresh", async (
            TokenRequest request,
            UserManager<IdentityUser> userManager,
            ApiDbContext db,
            IConfiguration config,
            HttpContext ctx) =>
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
                var user = await userManager.FindByIdAsync(refreshToken!.UserId.ToString());
                if (user == null)
                {
                    return Results.Json(new { error = "User not found" }, statusCode: 404);
                }

                // Revoke old refresh token
                RefreshTokenService.RevokeToken(refreshToken);

                // Generate new tokens
                var newAccessToken = GenerateJwtToken(user, config, expiryMinutes: 30);

                var device = ctx.Request.Headers.UserAgent.ToString();
                var ipAddress = ctx.Connection.RemoteIpAddress?.ToString();
                var newRefreshToken = RefreshTokenService.CreateRefreshToken(refreshToken.UserId, device, ipAddress, expiryDays: 14);

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
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        }).WithName("RefreshToken");

        // 🚪 LOGOUT (revoke refresh token)
        group.MapPost("/logout", async (
            RevokeTokenRequest request,
            ApiDbContext db) =>
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
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        }).WithName("Logout");

        // 🔒 REVOKE ALL TOKENS (logout from all devices)
        group.MapPost("/revoke-all", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            try
            {
                int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

                var userTokens = await db.RefreshTokens
                    .Where(t => t.UserId == userId && t.RevokedAt == null)
                    .ToListAsync();

                foreach (var token in userTokens)
                {
                    RefreshTokenService.RevokeToken(token);
                }

                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    message = $"Revoked {userTokens.Count} tokens",
                    revokedCount = userTokens.Count
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        })
        .RequireAuthorization()
        .WithName("RevokeAllTokens");

        // REGISTER (with refresh token)
        group.MapPost("/register", async (
            RegisterRequest request,
            UserManager<IdentityUser> userManager,
            ApiDbContext db,
            IConfiguration config,
            HttpContext ctx) =>
        {
            try
            {
                var existingUser = await userManager.FindByNameAsync(request.Username);
                if (existingUser != null)
                {
                    return Results.Json(new { error = "Username already exists" }, statusCode: 400);
                }

                var user = new IdentityUser
                {
                    UserName = request.Username,
                    Email = request.Email
                };

                var result = await userManager.CreateAsync(user, request.Password);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return Results.Json(new { error = errors }, statusCode: 400);
                }

                int userId;
                if (!int.TryParse(user.Id, out userId))
                {
                    userId = Math.Abs(user.Id.GetHashCode());
                }

                // Generate tokens
                var accessToken = GenerateJwtToken(user, config, expiryMinutes: 30);

                var device = ctx.Request.Headers.UserAgent.ToString();
                var ipAddress = ctx.Connection.RemoteIpAddress?.ToString();
                var refreshToken = RefreshTokenService.CreateRefreshToken(userId, device, ipAddress, expiryDays: 14);

                db.RefreshTokens.Add(refreshToken);
                await db.SaveChangesAsync();

                return Results.Ok(new TokenResponse(
                    AccessToken: accessToken,
                    RefreshToken: refreshToken.Token,
                    ExpiresIn: 1800,
                    UserId: userId,
                    Username: user.UserName ?? ""
                ));
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        // TEST endpoint (no auth required)
        group.MapGet("/test", () => Results.Ok(new
        {
            message = "Auth endpoints are working!",
            timestamp = DateTime.UtcNow
        })).WithName("TestAuth");
    }

    private static string GenerateJwtToken(IdentityUser user, IConfiguration config, int expiryMinutes = 30)
    {
        var jwtSettings = config.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
        var issuer = jwtSettings["Issuer"] ?? "MathLearningAPI";
        var audience = jwtSettings["Audience"] ?? "MathLearningApp";

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        int userId;
        if (!int.TryParse(user.Id, out userId))
        {
            userId = Math.Abs(user.Id.GetHashCode());
        }

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? ""),
            new Claim("userId", userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
