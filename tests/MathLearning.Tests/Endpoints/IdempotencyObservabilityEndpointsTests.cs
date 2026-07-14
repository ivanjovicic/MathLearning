using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Cosmetics;
using MathLearning.Infrastructure.Services.Idempotency;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class IdempotencyObservabilityEndpointsTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private const string AdminRole = "UiTokensAdmin";
    private const string MonitoringPath = "/api/monitoring/idempotency";
    private const string QuizEndpoint = "POST /api/quiz/answer";
    private const string EconomyEndpoint = "POST /api/economy/coins/spend";
    private const string CosmeticsEndpoint = "POST /api/cosmetics/items/{itemKey}/claim";

    private readonly HttpClient client;
    private readonly CustomWebApplicationFactory<Program> factory;

    public IdempotencyObservabilityEndpointsTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task QuizSnapshot_TracksFirstReplayConflictAndRollback()
    {
        ResetObservability();

        var successUserId = NewUserId("quiz-success");
        await EnsureUserAsync(successUserId);
        var successQuiz = await StartIssuedQuizAsync(successUserId);

        var successPayload = new
        {
            quizId = successQuiz.QuizId.ToString(),
            questionId = successQuiz.QuestionId,
            answer = "2",
            timeSpentSeconds = 5,
            operationId = "quiz-observe-1",
            idempotencyKey = "quiz-observe-1"
        };

        Assert.Equal(HttpStatusCode.OK, (await PostAsUserAsync(successUserId, "/api/quiz/answer", successPayload)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsUserAsync(successUserId, "/api/quiz/answer", successPayload)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsUserAsync(successUserId, "/api/quiz/answer", new
        {
            quizId = successQuiz.QuizId.ToString(),
            questionId = successQuiz.QuestionId,
            answer = "3",
            timeSpentSeconds = 5,
            operationId = "quiz-observe-1",
            idempotencyKey = "quiz-observe-1"
        })).StatusCode);

        var rollbackUserId = NewUserId("quiz-rollback");
        await EnsureUserWithoutProfileAsync(rollbackUserId);
        var rollbackQuiz = await StartIssuedQuizAsync(rollbackUserId);
        Assert.Equal(HttpStatusCode.InternalServerError, (await PostAsUserAsync(rollbackUserId, "/api/quiz/answer", new
        {
            quizId = rollbackQuiz.QuizId.ToString(),
            questionId = rollbackQuiz.QuestionId,
            answer = "2",
            timeSpentSeconds = 5,
            operationId = "quiz-observe-rollback",
            idempotencyKey = "quiz-observe-rollback"
        })).StatusCode);

        var snapshot = await GetAdminSnapshotAsync();
        Assert.Equal(1, GetCount(snapshot, "first_success", QuizEndpoint, QuizOperationTypes.QuizAnswer, "completed"));
        Assert.Equal(1, GetCount(snapshot, "replay", QuizEndpoint, QuizOperationTypes.QuizAnswer, IdempotencyLedgerStatuses.Completed));
        Assert.Equal(1, GetCount(snapshot, "conflict", QuizEndpoint, QuizOperationTypes.QuizAnswer, "conflict"));
        Assert.Equal(1, GetCount(snapshot, "rollback", QuizEndpoint, QuizOperationTypes.QuizAnswer, "rolled_back"));
    }

    [Fact]
    public async Task EconomyAndCosmeticsSnapshot_TracksReplayConflictAndFailure()
    {
        ResetObservability();

        var economyUserId = NewUserId("economy");
        await EnsureUserAsync(economyUserId, coins: 100);

        var economyPayload = new
        {
            operationId = "econ-observe-1",
            idempotencyKey = "econ-observe-1",
            amount = 10,
            reason = "hint"
        };

        Assert.Equal(HttpStatusCode.OK, (await PostAsUserAsync(economyUserId, "/api/economy/coins/spend", economyPayload)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsUserAsync(economyUserId, "/api/economy/coins/spend", economyPayload)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsUserAsync(economyUserId, "/api/economy/coins/spend", new
        {
            operationId = "econ-observe-1",
            idempotencyKey = "econ-observe-1",
            amount = 20,
            reason = "hint"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsUserAsync(economyUserId, "/api/economy/coins/spend", new
        {
            operationId = "econ-observe-fail",
            idempotencyKey = "econ-observe-fail",
            amount = 500,
            reason = "hint"
        })).StatusCode);

        var cosmeticsUserId = NewUserId("cosmetics");
        await EnsureUserAsync(cosmeticsUserId);
        await EnsureCosmeticItemAsync("frame_comet", "Comet Frame");

        Assert.Equal(HttpStatusCode.OK, (await PostAsUserAsync(cosmeticsUserId, "/api/cosmetics/items/frame_comet/claim", new
        {
            operationId = "cos-observe-1",
            idempotencyKey = "cos-observe-1",
            source = "reward",
            sourceType = "reward",
            sourceEvent = "level:7"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsUserAsync(cosmeticsUserId, "/api/cosmetics/items/frame_comet/claim", new
        {
            operationId = "cos-observe-1",
            idempotencyKey = "cos-observe-1",
            source = "reward",
            sourceType = "reward",
            sourceEvent = "level:7"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsUserAsync(cosmeticsUserId, "/api/cosmetics/items/frame_comet/claim", new
        {
            operationId = "cos-observe-1",
            idempotencyKey = "cos-observe-1",
            source = "reward",
            sourceType = "reward",
            sourceEvent = "milestone:2"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsUserAsync(cosmeticsUserId, "/api/cosmetics/items/missing-item/claim", new
        {
            operationId = "cos-observe-fail",
            idempotencyKey = "cos-observe-fail",
            source = "reward",
            sourceType = "reward",
            sourceEvent = "broken:1"
        })).StatusCode);

        var snapshot = await GetAdminSnapshotAsync();
        Assert.Equal(1, GetCount(snapshot, "first_success", EconomyEndpoint, "economy_coins_spend", "completed"));
        Assert.Equal(1, GetCount(snapshot, "replay", EconomyEndpoint, "economy_coins_spend", EconomyTransactionStatus.Completed.ToString()));
        Assert.Equal(1, GetCount(snapshot, "conflict", EconomyEndpoint, "economy_coins_spend", "conflict"));
        Assert.Equal(1, GetCount(snapshot, "failure", EconomyEndpoint, "economy_coins_spend", EconomyTransactionStatus.Failed.ToString()));

        Assert.Equal(1, GetCount(snapshot, "first_success", CosmeticsEndpoint, "cosmetics_item_claim", "completed"));
        Assert.Equal(1, GetCount(snapshot, "replay", CosmeticsEndpoint, "cosmetics_item_claim", "completed"));
        Assert.Equal(1, GetCount(snapshot, "conflict", CosmeticsEndpoint, "cosmetics_item_claim", "conflict"));
        Assert.Equal(1, GetCount(snapshot, "failure", CosmeticsEndpoint, "cosmetics_item_claim", "failed"));
    }

    private void ResetObservability()
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IdempotencyObservabilityService>().Reset();
    }

    private async Task<IdempotencyObservabilitySnapshot> GetAdminSnapshotAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, MonitoringPath);
        request.Headers.Add("X-Test-UserId", "admin-user");
        request.Headers.Add("X-Test-Roles", AdminRole);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdempotencyObservabilitySnapshot>())!;
    }

    private static long GetCount(
        IdempotencyObservabilitySnapshot snapshot,
        string category,
        string endpoint,
        string operationType,
        string status)
        => snapshot.Rows.SingleOrDefault(x =>
               x.Category == category &&
               x.Endpoint == endpoint &&
               x.OperationType == operationType &&
               x.Status == status)?.Count ?? 0;

    private async Task<HttpResponseMessage> PostAsUserAsync(string userId, string url, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Test-UserId", userId);
        return await client.SendAsync(request);
    }

    private async Task EnsureUserAsync(string userId, int coins = 0)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        if (await userManager.FindByIdAsync(userId) is null)
        {
            await userManager.CreateAsync(new IdentityUser
            {
                Id = userId,
                UserName = userId,
                Email = $"{userId}@example.test"
            });
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile is null)
        {
            profile = new UserProfile
            {
                UserId = userId,
                Username = userId,
                DisplayName = userId
            };
            db.UserProfiles.Add(profile);
        }

        profile.Coins = coins;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task EnsureUserWithoutProfileAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        if (await userManager.FindByIdAsync(userId) is null)
        {
            await userManager.CreateAsync(new IdentityUser
            {
                Id = userId,
                UserName = userId,
                Email = $"{userId}@example.test"
            });
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile is not null)
        {
            db.UserProfiles.Remove(profile);
            await db.SaveChangesAsync();
        }
    }

    private async Task<(Guid QuizId, int QuestionId)> StartIssuedQuizAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var subtopicId = await db.Questions.Select(q => q.SubtopicId).FirstAsync();

        var response = await PostAsUserAsync(userId, "/api/quiz/start", new
        {
            subtopicId,
            questionCount = 1
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (
            Guid.Parse(payload.GetProperty("quizId").GetString()!),
            payload.GetProperty("questions").EnumerateArray().Single().GetProperty("id").GetInt32());
    }

    private async Task EnsureCosmeticItemAsync(string key, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        if (await db.CosmeticItems.AnyAsync(x => x.Key == key))
            return;

        db.CosmeticItems.Add(new CosmeticItem
        {
            Key = key,
            Name = name,
            Category = "frame",
            Rarity = "rare",
            AssetPath = $"cosmetics/frame/{key}",
            UnlockType = CosmeticUnlockTypes.RewardRule,
            FragmentLabel = "Comet Frame Fragment",
            FragmentsRequired = 5,
            IsActive = true,
            AssetVersion = "1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static string NewUserId(string suffix) => $"idem-observe-{suffix}-{Guid.NewGuid():N}";
}
