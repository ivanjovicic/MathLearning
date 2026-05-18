using System.Net;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public class UserSettingsEndpointsIntegrationTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client;
    private ApiDbContext _dbContext;
    private const string TestUserId = "auth0|test-user-123";
    private const string OtherUserId = "auth0|other-user-456";

    public UserSettingsEndpointsIntegrationTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the existing DbContext
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApiDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    // Add in-memory database for testing
                    services.AddDbContext<ApiDbContext>(options =>
                        options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
                });
            });
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {CreateTestToken(TestUserId)}");

        using var scope = _factory.Services.CreateScope();
        _dbContext = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        // Seed test user profiles
        _dbContext.UserProfiles.AddRange(
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

        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetSettings_WithValidIdentityUserId_Returns200AndSettings()
    {
        // Act
        var response = await _client.GetAsync($"/api/users/{TestUserId}/settings");

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
        var response = await _client.GetAsync($"/api/users/{OtherUserId}/settings");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSettings_WithoutAuthToken_Returns401Unauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync($"/api/users/{TestUserId}/settings");

        // Assert
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
        var response = await _client.PatchAsync($"/api/users/{TestUserId}/settings", content);

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
        var response = await _client.PatchAsync($"/api/users/{TestUserId}/settings", content);

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
        var response = await _client.PatchAsync($"/api/users/{OtherUserId}/settings", content);

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
        var response = await _client.PatchAsync($"/api/users/{TestUserId}/settings", content);

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
        var response = await _client.PatchAsync($"/api/users/{TestUserId}/settings", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(responseContent).RootElement;
        Assert.Equal(languageCode, json.GetProperty("languageCode").GetString());
    }

    [Fact]
    public async Task GetSettings_CreatesDefaultSettingsIfNotExist()
    {
        // Arrange
        var userId = "auth0|new-user-789";
        var newUserProfile = new UserProfile
        {
            UserId = userId,
            Username = "newuser",
            DisplayName = "New User",
            Coins = 0,
            Level = 1,
            Xp = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.UserProfiles.Add(newUserProfile);
        await _dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {CreateTestToken(userId)}");

        // Act
        var response = await client.GetAsync($"/api/users/{userId}/settings");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content).RootElement;
        
        Assert.Equal(userId, json.GetProperty("userId").GetString());
        Assert.Equal("sr", json.GetProperty("language").GetString()); // Default
        Assert.Equal("light", json.GetProperty("theme").GetString()); // Default
        Assert.True(json.GetProperty("hintsEnabled").GetBoolean());
    }

    private static string CreateTestToken(string userId)
    {
        // In a real test, you'd use a proper JWT token generator
        // For this test, we're mocking the claim via test setup
        return "test-token";
    }
}
