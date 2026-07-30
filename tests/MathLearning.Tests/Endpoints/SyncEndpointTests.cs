using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Sync;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Sync;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MathLearning.Tests.Endpoints;

public sealed class SyncEndpointTests : IClassFixture<SyncEndpointLimitWebApplicationFactory>
{
    private readonly HttpClient client;

    public SyncEndpointTests(SyncEndpointLimitWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Theory]
    [InlineData(127)]
    [InlineData(128)]
    public async Task SyncPost_AtOrBelowBodyLimit_IsNotTransportRejected(int bodyLength)
    {
        var response = await PostRawBodyAsync(bodyLength);

        Assert.NotEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task SyncPost_AboveBodyLimit_Returns413WithSafeCode()
    {
        var response = await PostRawBodyAsync(129);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(text);
        Assert.Equal("request_too_large", json.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal("Request body is too large.", json.RootElement.GetProperty("error").GetString());
    }

    private async Task<HttpResponseMessage> PostRawBodyAsync(int bodyLength)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/sync")
        {
            Content = new StringContent(new string('a', bodyLength), Encoding.UTF8, "application/json")
        };

        return await client.SendAsync(request);
    }
}

[Collection("ThrowOnSaveDbContext")]
public sealed class SafeClientErrorTests : IClassFixture<SyncSafeErrorWebApplicationFactory>
{
    private readonly HttpClient client;
    private readonly SyncSafeErrorWebApplicationFactory factory;

    public SafeClientErrorTests(SyncSafeErrorWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task SyncPost_UnexpectedFailure_ReturnsSafeErrorWithoutSecretText()
    {
        ThrowOnSaveDbUpdateApiDbContext.ThrowOnSave = false;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            db.SyncDevices.Add(new SyncDevice
            {
                DeviceId = "device-safe",
                UserId = "1",
                DeviceName = "Safe Device",
                Platform = "android",
                AppVersion = "1.0.0",
                SecretKey = "safe-secret",
                Status = SyncDeviceStatuses.Active,
                RegisteredAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow
            });
            db.DeviceSyncStates.Add(new DeviceSyncState
            {
                DeviceId = "device-safe",
                UserId = "1",
                LastAcknowledgedEvent = 0,
                LastProcessedClientSequence = 0,
                LastSyncTimeUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        ThrowOnSaveDbUpdateApiDbContext.ThrowOnSave = true;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/sync")
            {
                Content = JsonContent.Create(new SyncRequestDto(
                    "device-safe",
                    0,
                    [new SyncOperationDto(
                        Guid.NewGuid(),
                        "device-safe",
                        "1",
                        1,
                        "submit_answer",
                        DateTime.UtcNow,
                        JsonSerializer.SerializeToElement(new SubmitAnswerSyncPayloadDto(
                            "session-safe",
                            1,
                            "wrong-answer",
                            5,
                            DateTime.UtcNow)),
                        null)]))
            };
            request.Headers.Add("X-Test-UserId", "1");

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("supersecret", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("stack", text, StringComparison.OrdinalIgnoreCase);

            using var json = JsonDocument.Parse(text);
            Assert.Equal("Internal server error.", json.RootElement.GetProperty("error").GetString());
            Assert.Equal("INTERNAL_ERROR", json.RootElement.GetProperty("errorCode").GetString());
            Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
        }
        finally
        {
            ThrowOnSaveDbUpdateApiDbContext.ThrowOnSave = false;
        }
    }
}

public sealed class SyncEndpointLimitWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<SyncOptions>(options =>
            {
                options.MaxRequestBodyBytes = 128;
                options.RequireOperationSignatures = false;
            });
        });
    }
}

public sealed class SyncSafeErrorWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    public SyncSafeErrorWebApplicationFactory()
    {
        ThrowOnSaveDbUpdateApiDbContext.ThrowOnSave = false;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApiDbContext>>();
            services.RemoveAll<ApiDbContext>();

            var dbName = $"sync-safe-errors-{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<ApiDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            services.AddSingleton(options);
            services.AddScoped<ApiDbContext, ThrowOnSaveDbUpdateApiDbContext>();
            services.PostConfigure<SyncOptions>(options =>
            {
                options.RequireOperationSignatures = false;
            });
        });
    }
}

[CollectionDefinition("ThrowOnSaveDbContext", DisableParallelization = true)]
public sealed class ThrowOnSaveDbContextCollectionDefinition
{
}

internal sealed class ThrowOnSaveDbUpdateApiDbContext : ApiDbContext
{
    public static bool ThrowOnSave { get; set; }

    public ThrowOnSaveDbUpdateApiDbContext(DbContextOptions<ApiDbContext> options)
        : base(options)
    {
    }

    public override int SaveChanges()
    {
        if (ThrowOnSave)
        {
            throw CreateException();
        }

        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ThrowOnSave)
        {
            throw CreateException();
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    private static DbUpdateException CreateException() =>
        new("SECRET_SYNC_SAVE_FAILURE", new InvalidOperationException("password=supersecret token=abc123 sync save failed"));
}
