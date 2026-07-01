using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Api.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class LegacyAvatarUploadSafetyTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private const string OwnerUserId = "1001";
    private const string OtherUserId = "1002";

    private static readonly byte[] MinimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];

    private readonly CustomWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public LegacyAvatarUploadSafetyTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_UnsupportedExtension_ReturnsBadRequest()
    {
        await EnsureUserWithProfileAsync(OwnerUserId);

        var response = await UploadAsync(
            OwnerUserId,
            bytes: MinimalPng,
            fileName: "avatar.exe",
            contentType: "application/octet-stream");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unsupported file type", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Upload_OversizedFile_ReturnsBadRequest()
    {
        await EnsureUserWithProfileAsync(OwnerUserId);

        var oversized = new byte[LegacyAvatarUploadValidator.MaxFileBytes + 1];
        MinimalPng.CopyTo(oversized, 0);

        var response = await UploadAsync(
            OwnerUserId,
            bytes: oversized,
            fileName: "avatar.png",
            contentType: "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("maximum allowed size", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Upload_SpoofedContentType_ReturnsBadRequest()
    {
        await EnsureUserWithProfileAsync(OwnerUserId);

        var response = await UploadAsync(
            OwnerUserId,
            bytes: MinimalPng,
            fileName: "avatar.png",
            contentType: "image/jpeg");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("does not match", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Upload_ValidPng_CanBeFetchedByOwner_ButNotViaStaticFiles()
    {
        await EnsureUserWithProfileAsync(OwnerUserId);

        var uploadResponse = await UploadAsync(
            OwnerUserId,
            bytes: MinimalPng,
            fileName: "avatar.png",
            contentType: "image/png");

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        using var uploadJson = JsonDocument.Parse(await uploadResponse.Content.ReadAsStringAsync());
        var avatarUrl = uploadJson.RootElement.GetProperty("avatarUrl").GetString();
        Assert.False(string.IsNullOrWhiteSpace(avatarUrl));

        var fileName = avatarUrl!.Split('/').Last();
        var ownerFetch = await GetAvatarAsync(OwnerUserId, OwnerUserId, fileName);
        Assert.Equal(HttpStatusCode.OK, ownerFetch.StatusCode);

        var staticFetch = await client.GetAsync($"/uploads/avatars/{fileName}");
        Assert.Equal(HttpStatusCode.NotFound, staticFetch.StatusCode);
    }

    [Fact]
    public async Task GetAvatar_AnotherUsersRoute_ReturnsForbidden()
    {
        await EnsureUserWithProfileAsync(OwnerUserId);
        await EnsureUserWithProfileAsync(OtherUserId);

        var uploadResponse = await UploadAsync(
            OwnerUserId,
            bytes: MinimalPng,
            fileName: "avatar.png",
            contentType: "image/png");

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        using var uploadJson = JsonDocument.Parse(await uploadResponse.Content.ReadAsStringAsync());
        var fileName = uploadJson.RootElement.GetProperty("avatarUrl").GetString()!.Split('/').Last();

        var response = await GetAvatarAsync(OtherUserId, OwnerUserId, fileName);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<HttpResponseMessage> UploadAsync(
        string userId,
        byte[] bytes,
        string fileName,
        string contentType)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/users/{userId}/avatar")
        {
            Content = content
        };
        request.Headers.Add("X-Test-UserId", userId);
        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetAvatarAsync(string callerUserId, string routeUserId, string fileName)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/users/{routeUserId}/avatar/{fileName}");
        request.Headers.Add("X-Test-UserId", callerUserId);
        return await client.SendAsync(request);
    }

    private async Task EnsureUserWithProfileAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        if (await userManager.FindByIdAsync(userId) is null)
        {
            await userManager.CreateAsync(new IdentityUser
            {
                Id = userId,
                UserName = $"user-{userId}",
                Email = $"user-{userId}@test.local"
            });
        }

        if (!await db.UserProfiles.AnyAsync(p => p.UserId == userId))
        {
            var now = DateTime.UtcNow;
            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Username = $"user-{userId}",
                DisplayName = $"User {userId}",
                Coins = 0,
                Level = 1,
                Xp = 0,
                Streak = 0,
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }
    }
}
