using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Api.Services;
using MathLearning.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class AuthPasswordResetContractTests : IClassFixture<CustomWebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public AuthPasswordResetContractTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var environment = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
        var seeder = ActivatorUtilities.CreateInstance<MathLearning.Api.Startup.TestAccountSeeder>(scope.ServiceProvider);
        await seeder.SeedAsync(environment);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Login_UnknownUserAndWrongPassword_UseSameSafeContract()
    {
        var unknown = await client.PostAsJsonAsync(
            "/auth/login",
            new { username = $"missing-{Guid.NewGuid():N}", password = "wrong-passphrase-2026!" });
        var wrongPassword = await client.PostAsJsonAsync(
            "/auth/login",
            new { username = "test", password = "wrong-passphrase-2026!" });

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        var unknownBody = await ReadJsonAsync(unknown);
        var wrongBody = await ReadJsonAsync(wrongPassword);
        Assert.Equal("invalid_credentials", unknownBody.GetProperty("code").GetString());
        Assert.Equal("invalid_credentials", wrongBody.GetProperty("code").GetString());
        Assert.Equal(unknownBody.GetProperty("message").GetString(), wrongBody.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ForgotPassword_IsGenericButDeliversOnlyForExistingAccount()
    {
        var existingEmail = $"reset-{Guid.NewGuid():N}@mathlearning.local";
        var password = "MathLearningPassphrase2026!";
        using (var scope = factory.Services.CreateScope())
        {
            var provisioning = scope.ServiceProvider.GetRequiredService<IAccountProvisioningService>();
            var result = await provisioning.CreateCompleteAccountAsync(
                $"reset-{Guid.NewGuid():N}", existingEmail, password, "Reset User");
            Assert.True(result.Succeeded);
        }

        var missingEmail = $"missing-{Guid.NewGuid():N}@mathlearning.local";
        var existing = await client.PostAsJsonAsync("/auth/password/forgot", new { email = existingEmail });
        var missing = await client.PostAsJsonAsync("/auth/password/forgot", new { email = missingEmail });

        Assert.Equal(HttpStatusCode.Accepted, existing.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, missing.StatusCode);
        var existingBody = await ReadJsonAsync(existing);
        var missingBody = await ReadJsonAsync(missing);
        Assert.Equal(existingBody.GetProperty("code").GetString(), missingBody.GetProperty("code").GetString());
        Assert.Equal(existingBody.GetProperty("message").GetString(), missingBody.GetProperty("message").GetString());
        Assert.DoesNotContain("token", (await existing.Content.ReadAsStringAsync()).ToLowerInvariant());

        using var deliveryScope = factory.Services.CreateScope();
        var delivery = deliveryScope.ServiceProvider.GetRequiredService<IPasswordResetDelivery>();
        var inMemory = Assert.IsType<InMemoryPasswordResetDelivery>(delivery);
        Assert.Contains(inMemory.Messages, message => message.RecipientEmail == existingEmail);
        Assert.DoesNotContain(inMemory.Messages, message => message.RecipientEmail == missingEmail);
    }

    [Fact]
    public async Task ResetPassword_ChangesPasswordAndInvalidatesExistingRefreshSession()
    {
        var username = $"reset-{Guid.NewGuid():N}";
        var email = $"{username}@mathlearning.local";
        var oldPassword = "MathLearningPassphrase2026!";
        var newPassword = "NewMathLearningPassphrase2026!";
        using (var scope = factory.Services.CreateScope())
        {
            var provisioning = scope.ServiceProvider.GetRequiredService<IAccountProvisioningService>();
            var result = await provisioning.CreateCompleteAccountAsync(username, email, oldPassword, username);
            Assert.True(result.Succeeded);
        }

        var login = await client.PostAsJsonAsync("/auth/login", new { username, password = oldPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await ReadJsonAsync(login);
        var oldRefreshToken = loginBody.GetProperty("refreshToken").GetString();

        var forgot = await client.PostAsJsonAsync("/auth/password/forgot", new { email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        using var deliveryScope = factory.Services.CreateScope();
        var delivery = Assert.IsType<InMemoryPasswordResetDelivery>(
            deliveryScope.ServiceProvider.GetRequiredService<IPasswordResetDelivery>());
        var message = Assert.Single(delivery.Messages.Where(item => item.RecipientEmail == email));

        var reset = await client.PostAsJsonAsync(
            "/auth/password/reset",
            new { email, token = message.ResetToken, newPassword });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var resetBody = await ReadJsonAsync(reset);
        Assert.Equal("password_reset_success", resetBody.GetProperty("code").GetString());

        var refresh = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = oldRefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        var oldLogin = await client.PostAsJsonAsync("/auth/login", new { username, password = oldPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var newLogin = await client.PostAsJsonAsync("/auth/login", new { username, password = newPassword });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        var replay = await client.PostAsJsonAsync(
            "/auth/password/reset",
            new { email, token = message.ResetToken, newPassword = "AnotherMathLearningPassphrase2026!" });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        var replayBody = await ReadJsonAsync(replay);
        Assert.Equal("password_reset_invalid", replayBody.GetProperty("code").GetString());
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
