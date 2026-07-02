using System.Net;
using System.Net.Http.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class QuestionAuthoringAuthorizationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public QuestionAuthoringAuthorizationTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/questions/save-draft")]
    [InlineData("/api/questions/publish")]
    public async Task Learner_CannotMutateAuthoringRoutes(string path)
    {
        var payload = path.EndsWith("publish", StringComparison.Ordinal)
            ? (object)new PublishQuestionRequest(Guid.NewGuid(), "forbidden")
            : new SaveQuestionDraftRequest(CreateValidRequest(), "forbidden");

        var response = await PostAsUserAsync("learner-user", roles: null, path, payload);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Learner_CannotRevalidatePublishedQuestion()
    {
        var response = await PostAsUserAsync(
            "learner-user",
            roles: null,
            "/api/questions/1/revalidate",
            new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Learner_ForbiddenSaveDraft_DoesNotCreateDraftRows()
    {
        var before = await CountDraftsAsync();

        var response = await PostAsUserAsync(
            "learner-user",
            roles: null,
            "/api/questions/save-draft",
            new SaveQuestionDraftRequest(CreateValidRequest(questionId: null), "forbidden"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, await CountDraftsAsync());
    }

    [Fact]
    public async Task ContentAuthor_CanSaveDraft()
    {
        var response = await PostAsUserAsync(
            "content-author-user",
            DesignTokenSecurity.ContentAuthorRole,
            "/api/questions/save-draft",
            new SaveQuestionDraftRequest(CreateValidRequest(questionId: null), "author"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanValidatePreviewAndPublish()
    {
        var validate = await PostAsUserAsync(
            "admin-author-user",
            DesignTokenSecurity.AdminRole,
            "/api/questions/validate",
            CreateValidRequest());
        Assert.Equal(HttpStatusCode.OK, validate.StatusCode);

        var preview = await PostAsUserAsync(
            "admin-author-user",
            DesignTokenSecurity.AdminRole,
            "/api/questions/preview",
            CreateValidRequest());
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);

        var save = await PostAsUserAsync(
            "admin-author-user",
            DesignTokenSecurity.AdminRole,
            "/api/questions/save-draft",
            new SaveQuestionDraftRequest(CreateValidRequest(questionId: null), "admin"));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var savePayload = await save.Content.ReadFromJsonAsync<SaveQuestionDraftResponse>();
        Assert.NotNull(savePayload);

        var publish = await PostAsUserAsync(
            "admin-author-user",
            DesignTokenSecurity.AdminRole,
            "/api/questions/publish",
            new PublishQuestionRequest(savePayload.DraftId, "admin publish"));
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
    }

    private async Task<int> CountDraftsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.QuestionDrafts.CountAsync();
    }

    private async Task<HttpResponseMessage> PostAsUserAsync(
        string userId,
        string? roles,
        string path,
        object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Test-UserId", userId);
        if (!string.IsNullOrWhiteSpace(roles))
            request.Headers.Add("X-Test-Roles", roles);

        return await client.SendAsync(request);
    }

    private static QuestionAuthoringRequest CreateValidRequest(int? questionId = 1)
        => new(
            questionId,
            "Koliko je $1+1$?",
            "multiple_choice",
            "$2$",
            "Saberi brojeve.",
            2,
            1,
            1,
            [
                new QuestionAuthoringOptionDto(1, "$2$", true),
                new QuestionAuthoringOptionDto(2, "$3$", false),
                new QuestionAuthoringOptionDto(3, "$4$", false)
            ],
            [],
            [new StepExplanationAuthoringDto(1, "$1+1=2$", null, false)],
            "authorization-test");
}
