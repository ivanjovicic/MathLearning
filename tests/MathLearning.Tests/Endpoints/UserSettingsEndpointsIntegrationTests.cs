using System.Net;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public class UserSettingsEndpointsIntegrationTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;
    private const string TestUserId = "auth0|test-user-123";
    private const string OtherUserId = "auth0|other-user-456";

    public UserSettingsEndpointsIntegrationTests()
    {
        _factory = new CustomWebApplicationFactory<Program>();
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Test-UserId", TestUserId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        await db.Database.EnsureCreatedAsync();

        if (!await db.Users.AnyAsync(x => x.Id == TestUserId))
        {
            db.Users.Add(new IdentityUser { Id = TestUserId, UserName = TestUserId, Email = "test@example.test" });
        }
        if (!await db.Users.AnyAsync(x => x.Id == OtherUserId))
        {
            db.Users.Add(new IdentityUser { Id = OtherUserId, UserName = OtherUserId, Email = "other@example.test" });
        }

        db.UserProfiles.AddRange(
            new UserProfile
            {
                UserId = TestUserId,
                Username = "testuser",
                DisplayName = "Test User",
                Coins = 0,
                Level = 1,
                Xp = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new UserProfile
            {
                UserId = OtherUserId,
                Username = "otheruser",
                DisplayName = "Other User",
                Coins = 0,
                Level = 1,
                Xp = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetSettings_WithValidIdentityUserId_Returns200AndSettings()
    {
        // Act
        var response = await _client.GetAsync($"/users/{TestUserId}/settings");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content).RootElement;

        Assert.Equal(TestUserId, json.GetProperty("userId").GetString());
        Assert.Equal("sr", json.GetProperty("language").GetString());
        Assert.Equal("light", json.GetProperty("theme").GetString());
        Assert.True(json.GetProperty("hintsEnabled").GetBoolean());
        Assert.True(json.GetProperty("soundEnabled").GetBoolean());
    }

    [Fact]
    public async Task GetSettings_WithDifferentUserId_Returns403Forbidden()
    {
        // Act
        var response = await _client.GetAsync($"/users/{OtherUserId}/settings");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSettings_WithoutAuthToken_Returns401Unauthorized()
    {
        var unauthenticatedClient = new AnonymousWebApplicationFactory().CreateClient();

        var response = await unauthenticatedClient.GetAsync($"/users/{TestUserId}/settings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchSettings_WithLanguageCode_Returns200AndPersistsChange()
    {
        // Arrange
        var updateRequest = new { languageCode = "en" };
        var content = new StringContent(
            JsonSerializer.Serialize(updateRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PatchAsync($"/users/{TestUserId}/settings", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(responseContent).RootElement;
        Assert.Equal("en", json.GetProperty("languageCode").GetString());

        // Verify persistence
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var settings = await dbContext.UserSettings.FirstOrDefaultAsync(s => s.UserId == TestUserId);
        Assert.NotNull(settings);
        Assert.Equal("en", settings.LanguageCode);
    }

    [Fact]
    public async Task PatchSettings_WithInvalidLanguageCode_Returns400BadRequest()
    {
        // Arrange
        var updateRequest = new { languageCode = "invalid-code" };
        var content = new StringContent(
            JsonSerializer.Serialize(updateRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PatchAsync($"/users/{TestUserId}/settings", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid languageCode", responseContent);
    }

    [Fact]
    public async Task PatchSettings_WithDifferentUserId_Returns403Forbidden()
    {
        // Arrange
        var updateRequest = new { languageCode = "en" };
        var content = new StringContent(
            JsonSerializer.Serialize(updateRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PatchAsync($"/users/{OtherUserId}/settings", content);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatchSettings_WithMultipleSettings_Returns200AndPersistsAllChanges()
    {
        // Arrange
        var updateRequest = new
        {
            languageCode = "de",
            theme = "dark",
            hintsEnabled = false,
            soundEnabled = false,
            vibrationEnabled = false
        };
        var content = new StringContent(
            JsonSerializer.Serialize(updateRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PatchAsync($"/users/{TestUserId}/settings", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(responseContent).RootElement;
        
        Assert.Equal("de", json.GetProperty("languageCode").GetString());
        Assert.Equal("dark", json.GetProperty("theme").GetString());
        Assert.False(json.GetProperty("hintsEnabled").GetBoolean());
        Assert.False(json.GetProperty("soundEnabled").GetBoolean());
        Assert.False(json.GetProperty("vibrationEnabled").GetBoolean());

        // Verify persistence
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var settings = await dbContext.UserSettings.FirstOrDefaultAsync(s => s.UserId == TestUserId);
        Assert.NotNull(settings);
        Assert.Equal("de", settings.LanguageCode);
        Assert.Equal("dark", settings.Theme);
        Assert.False(settings.HintsEnabled);
        Assert.False(settings.SoundEnabled);
        Assert.False(settings.VibrationEnabled);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("sr")]
    [InlineData("de")]
    [InlineData("es")]
    public async Task PatchSettings_WithValidLanguageCodes_Returns200(string languageCode)
    {
        // Arrange
        var updateRequest = new { languageCode };
        var content = new StringContent(
            JsonSerializer.Serialize(updateRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PatchAsync($"/users/{TestUserId}/settings", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(responseContent).RootElement;
        Assert.Equal(languageCode, json.GetProperty("languageCode").GetString());
    }

    [Fact]
    public async Task GetSettings_CreatesDefaultSettingsIfNotExist()
    {
        var userId = "auth0|new-user-789";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            db.Users.Add(new IdentityUser { Id = userId, UserName = userId, Email = "new@example.test" });
            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Username = "newuser",
                DisplayName = "New User",
                Coins = 0,
                Level = 1,
                Xp = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);

        var response = await client.GetAsync($"/users/{userId}/settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content).RootElement;

        Assert.Equal(userId, json.GetProperty("userId").GetString());
        Assert.Equal("sr", json.GetProperty("language").GetString());
        Assert.Equal("light", json.GetProperty("theme").GetString());
        Assert.True(json.GetProperty("hintsEnabled").GetBoolean());
    }

    private sealed class AnonymousWebApplicationFactory : CustomWebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication("Anonymous")
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, NoAuthHandler>(
                        "Anonymous",
                        _ => { });
            });
        }
    }

    private sealed class NoAuthHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>
    {
        public NoAuthHandler(
            Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
            Microsoft.Extensions.Logging.ILoggerFactory logger,
            System.Text.Encodings.Web.UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
        {
            return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
        }
    }
}
