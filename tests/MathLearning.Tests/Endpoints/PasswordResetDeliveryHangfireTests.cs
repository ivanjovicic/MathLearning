using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using MathLearning.Api.Services;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MathLearning.Tests.Endpoints;

public sealed class PasswordResetDeliveryHangfireTests
{
    [Fact]
    public async Task ProductionDispatcher_EnqueuesOnlyTheIdentityUserId()
    {
        var jobs = new RecordingBackgroundJobClient();
        var dispatcher = new HangfirePasswordResetDeliveryDispatcher(jobs);
        const string userId = "identity-user-id";

        await dispatcher.DispatchAsync(userId);

        Assert.NotNull(jobs.CreatedJob);
        var job = jobs.CreatedJob!;
        Assert.Equal(nameof(IPasswordResetDeliveryJob.SendAsync), job.Method.Name);
        var arguments = Assert.Single(job.Args);
        Assert.IsType<string>(arguments);
        Assert.Equal(userId, arguments);
        Assert.DoesNotContain("PasswordResetDeliveryMessage", JsonSerializer.Serialize(job.Args), StringComparison.Ordinal);
        Assert.DoesNotContain("reset-token", JsonSerializer.Serialize(job.Args), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JobGeneratesTokenAtExecutionAndDeliveredUrlResetsPassword()
    {
        var factory = new PasswordResetRelationalWebApplicationFactory();
        try
        {
            using (var client = factory.CreateClient())
            {
                var account = await CreateAccountAsync(factory);

                using (var scope = factory.Services.CreateScope())
                {
                    var job = scope.ServiceProvider.GetRequiredService<IPasswordResetDeliveryJob>();
                    await job.SendAsync(account.UserId);
                }

                using (var deliveryScope = factory.Services.CreateScope())
                {
                    var delivery = Assert.IsType<InMemoryPasswordResetDelivery>(
                        deliveryScope.ServiceProvider.GetRequiredService<IPasswordResetDelivery>());
                    var message = Assert.Single(delivery.Messages);
                    var query = QueryHelpers.ParseQuery(new Uri(message.ResetUrl).Query);
                    Assert.Equal(message.RecipientEmail, query["email"].ToString());
                    Assert.Equal(message.ResetToken, query["token"].ToString());

                    var reset = await client.PostAsJsonAsync(
                        "/auth/password/reset",
                        new
                        {
                            email = query["email"].ToString(),
                            token = query["token"].ToString(),
                            newPassword = account.NewPassword
                        });
                    Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

                    var login = await client.PostAsJsonAsync(
                        "/auth/login",
                        new { username = account.Username, password = account.NewPassword });
                    Assert.Equal(HttpStatusCode.OK, login.StatusCode);
                }
            }
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabaseFiles();
        }
    }

    [Fact]
    public async Task JobForDeletedUserExitsWithoutDelivery()
    {
        var factory = new PasswordResetRelationalWebApplicationFactory();
        try
        {
            var account = await CreateAccountAsync(factory);
            using (var scope = factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser>>();
                var user = await userManager.FindByIdAsync(account.UserId);
                Assert.NotNull(user);
                var deleteResult = await userManager.DeleteAsync(user!);
                Assert.True(deleteResult.Succeeded);

                var job = scope.ServiceProvider.GetRequiredService<IPasswordResetDeliveryJob>();
                await job.SendAsync(account.UserId);

                var delivery = Assert.IsType<InMemoryPasswordResetDelivery>(
                    scope.ServiceProvider.GetRequiredService<IPasswordResetDelivery>());
                Assert.Empty(delivery.Messages);
            }
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabaseFiles();
        }
    }

    [Fact]
    public async Task DeliveryFailureLogsAndThrowsWithoutTokenOrUrl()
    {
        var factory = new PasswordResetRelationalWebApplicationFactory();
        try
        {
            var account = await CreateAccountAsync(factory);
            using (var scope = factory.Services.CreateScope())
            {
                var delivery = new ThrowingPasswordResetDelivery();
                var logger = new CapturingLogger<PasswordResetDeliveryJob>();
                var job = new PasswordResetDeliveryJob(
                    scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser>>(),
                    scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PasswordResetDeliveryOptions>>(),
                    delivery,
                    logger);

                var exception = await Assert.ThrowsAsync<PasswordResetDeliveryException>(
                    () => job.SendAsync(account.UserId));
                Assert.NotNull(delivery.Message);
                var message = delivery.Message!;
                var captured = string.Join("\n", logger.Messages);

                Assert.DoesNotContain(message.ResetToken, captured, StringComparison.Ordinal);
                Assert.DoesNotContain(message.ResetUrl, captured, StringComparison.Ordinal);
                Assert.DoesNotContain(message.ResetToken, exception.ToString(), StringComparison.Ordinal);
                Assert.DoesNotContain(message.ResetUrl, exception.ToString(), StringComparison.Ordinal);
            }
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabaseFiles();
        }
    }

    private static async Task<TestAccount> CreateAccountAsync(PasswordResetRelationalWebApplicationFactory factory)
    {
        var username = $"delivery-{Guid.NewGuid():N}";
        var email = $"{username}@mathlearning.local";
        const string oldPassword = "OldMathLearningPassphrase2026!";
        const string newPassword = "NewMathLearningPassphrase2026!";

        using var scope = factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<IAccountProvisioningService>();
        var result = await provisioning.CreateCompleteAccountAsync(username, email, oldPassword, username);
        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        return new TestAccount(result.User!.Id, username, email, newPassword);
    }

    private sealed record TestAccount(string UserId, string Username, string Email, string NewPassword);

    private sealed class RecordingBackgroundJobClient : IBackgroundJobClient
    {
        public Job? CreatedJob { get; private set; }

        public string Create(Job job, IState state)
        {
            CreatedJob = job;
            return Guid.NewGuid().ToString("N");
        }

        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }

    private sealed class ThrowingPasswordResetDelivery : IPasswordResetDelivery
    {
        public PasswordResetDeliveryMessage? Message { get; private set; }

        public Task<bool> SendAsync(PasswordResetDeliveryMessage message, CancellationToken cancellationToken = default)
        {
            Message = message;
            throw new InvalidOperationException($"SMTP rejected {message.ResetUrl}");
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NoopScope : IDisposable
        {
            public static NoopScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
