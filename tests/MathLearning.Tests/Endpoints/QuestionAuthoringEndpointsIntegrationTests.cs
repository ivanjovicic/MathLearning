using System.Net;
using System.Net.Http.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace MathLearning.Tests.Endpoints;

public class QuestionAuthoringEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient client;
    private readonly CustomWebApplicationFactory<Program> factory;

    public QuestionAuthoringEndpointsIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "authoring-admin-user");
        client.DefaultRequestHeaders.Add("X-Test-Roles", DesignTokenSecurity.AdminRole);
    }

    [Fact]
    public async Task Validate_ReturnsValidationSummary()
    {
        var response = await client.PostAsJsonAsync("/api/questions/validate", CreateValidRequest());

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ValidateQuestionResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.Summary.CanPublish);
        Assert.NotEmpty(payload.Normalized.Fields);
    }

    [Fact]
    public async Task Validate_XssAndMalformedLatex_ReturnsUnsafeMarkupAndLatexIssues()
    {
        var request = CreateValidRequest() with
        {
            Text = @"Koliko je $\frac{1}{?$<script>alert('xss')</script>"
        };

        var response = await client.PostAsJsonAsync("/api/questions/validate", request);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ValidateQuestionResponse>();

        Assert.NotNull(payload);
        Assert.False(payload.Summary.CanPublish);
        Assert.Contains(payload.Summary.Issues, issue => issue.RuleId == "content.unsafe_markup");
        Assert.Contains(payload.Summary.Issues, issue =>
            issue.RuleId is "latex.unbalanced_braces" or "latex.invalid_fraction");
        Assert.All(payload.Normalized.Fields, field =>
            Assert.DoesNotContain("<script", field.NormalizedValue, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Preview_ReturnsPreviewPayload()
    {
        var response = await client.PostAsJsonAsync("/api/questions/preview", CreateValidRequest());

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PreviewQuestionResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.Preview.Summary.CanPublish);
        Assert.Contains("text", payload.Preview.SafePreviewFields.Keys);
    }

    [Fact]
    public async Task Preview_SanitizesProvidedSemanticsAltText_AndUsesSafeFallbackForMalformedLatex()
    {
        var request = CreateValidRequest() with
        {
            Text = @"Koliko je $\frac{1}{?$<script>alert('xss')</script>",
            SemanticsAltText = @"<strong>Pitanje</strong><script>alert(1)</script>",
            Options =
            [
                new QuestionAuthoringOptionDto(1, "$2$", true, SemanticsAltText: "<em>Tacan</em><script>alert(2)</script>"),
                new QuestionAuthoringOptionDto(2, "$3$", false, SemanticsAltText: "<b>Netacan</b>"),
                new QuestionAuthoringOptionDto(3, "$4$", false, SemanticsAltText: "<img src=x onerror=alert(3)>Cetiri")
            ],
            Hints =
            [
                new QuestionHintDto("formula", "$a+b$", "<span>Formula</span><script>alert(4)</script>"),
                new QuestionHintDto("clue", "Pogledaj sabiranje.", "<b>Trag</b>"),
                new QuestionHintDto("full", "Rezultat je 2.", "<img src=x onerror=alert(5)>Pun hint")
            ],
            Steps =
            [
                new StepExplanationAuthoringDto(1, "$1+1=2$", null, false, SemanticsAltText: "<strong>Korak</strong><script>alert(6)</script>")
            ]
        };

        var response = await client.PostAsJsonAsync("/api/questions/preview", request);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PreviewQuestionResponse>();

        Assert.NotNull(payload);
        Assert.False(payload.Preview.Summary.CanPublish);
        Assert.DoesNotContain("<script", payload.Preview.Raw.SemanticsAltText ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Pitanje", payload.Preview.Raw.SemanticsAltText);
        Assert.All(payload.Preview.Raw.Options, option =>
            Assert.DoesNotContain("<", option.SemanticsAltText ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        Assert.All(payload.Preview.Raw.Hints, hint =>
            Assert.DoesNotContain("<", hint.SemanticsAltText ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        Assert.All(payload.Preview.Raw.Steps, step =>
            Assert.DoesNotContain("<", step.SemanticsAltText ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        Assert.True(payload.Preview.SafePreviewFields.TryGetValue("text", out var safeText));
        Assert.NotNull(safeText);
        Assert.DoesNotContain("<script", safeText!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"\frac", safeText!, StringComparison.Ordinal);
        Assert.Contains("Koliko je", safeText!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveDraft_ThenPublish_ReturnsPublishedVersion()
    {
        var saveResponse = await client.PostAsJsonAsync(
            "/api/questions/save-draft",
            new SaveQuestionDraftRequest(CreateValidRequest(questionId: null), "integration"));

        saveResponse.EnsureSuccessStatusCode();
        var savePayload = await saveResponse.Content.ReadFromJsonAsync<SaveQuestionDraftResponse>();
        Assert.NotNull(savePayload);

        var publishResponse = await client.PostAsJsonAsync(
            "/api/questions/publish",
            new PublishQuestionRequest(savePayload.DraftId, "integration publish"));

        publishResponse.EnsureSuccessStatusCode();
        var publishPayload = await publishResponse.Content.ReadFromJsonAsync<PublishQuestionResponse>();
        Assert.NotNull(publishPayload);
        Assert.True(publishPayload.Published);
        Assert.Equal("published", publishPayload.PublishState);
    }

    [Fact]
    public async Task Publish_PersistsConsistentDraftValidationPreviewAndVersionState()
    {
        var request = CreateValidRequest(questionId: null) with
        {
            Text = "Koliko je $2+2$?",
            CorrectAnswer = "$4$",
            Explanation = "Saberi dva i dva.",
            Options =
            [
                new QuestionAuthoringOptionDto(null, "$4$", true),
                new QuestionAuthoringOptionDto(null, "$3$", false),
                new QuestionAuthoringOptionDto(null, "$5$", false)
            ]
        };

        var saveResponse = await client.PostAsJsonAsync(
            "/api/questions/save-draft",
            new SaveQuestionDraftRequest(request, "consistency check"));
        saveResponse.EnsureSuccessStatusCode();
        var savePayload = await saveResponse.Content.ReadFromJsonAsync<SaveQuestionDraftResponse>();

        Assert.NotNull(savePayload);

        var publishResponse = await client.PostAsJsonAsync(
            "/api/questions/publish",
            new PublishQuestionRequest(savePayload.DraftId, "consistency publish"));
        publishResponse.EnsureSuccessStatusCode();
        var publishPayload = await publishResponse.Content.ReadFromJsonAsync<PublishQuestionResponse>();

        Assert.NotNull(publishPayload);
        Assert.True(publishPayload.Published);

        await using var db = CreateApiDbContext();
        var draft = await db.QuestionDrafts
            .Include(x => x.LatestValidationResult)
            .ThenInclude(x => x!.Issues)
            .SingleAsync(x => x.Id == savePayload.DraftId);
        var version = await db.QuestionVersions.SingleAsync(x => x.Id == publishPayload.VersionId);
        var question = await db.Questions
            .Include(x => x.Options)
            .SingleAsync(x => x.Id == publishPayload.QuestionId);
        var previewCache = await db.QuestionPreviewCaches.SingleAsync(x => x.DraftId == draft.Id);

        Assert.Equal(question.Id, draft.QuestionId);
        Assert.Equal(question.CurrentDraftId, draft.Id);
        Assert.Equal(QuestionPublishStates.Published, draft.PublishState);
        Assert.Equal(QuestionValidationStatuses.Passed, draft.ValidationStatus);
        Assert.NotNull(draft.LatestValidationResult);
        Assert.Equal(draft.ContentHash, draft.LatestValidationResult!.ContentHash);
        Assert.Equal(QuestionValidationStatuses.Passed, draft.LatestValidationResult.Status);
        Assert.Equal(draft.LatestValidationResultId, draft.LatestValidationResult.Id);

        Assert.Equal(question.Id, version.QuestionId);
        Assert.Equal(draft.Id, version.SourceDraftId);
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal(QuestionPublishStates.Published, version.PublishState);
        Assert.Equal(question.CurrentVersionNumber, version.VersionNumber);

        var draftRequest = JsonSerializer.Deserialize<QuestionAuthoringRequest>(draft.ContentJson, JsonOptions);
        var versionRequest = JsonSerializer.Deserialize<QuestionAuthoringRequest>(version.SnapshotJson, JsonOptions);
        Assert.NotNull(draftRequest);
        Assert.NotNull(versionRequest);
        Assert.Equal(draft.ContentJson, version.SnapshotJson);
        Assert.Equal(draft.NormalizedContentJson, version.NormalizedSnapshotJson);
        Assert.Equal(request.Text, draftRequest!.Text);
        Assert.Equal(draftRequest.Text, question.Text);
        Assert.Equal(versionRequest!.CorrectAnswer, question.CorrectAnswer);
        Assert.Equal(versionRequest.Options.Single(x => x.IsCorrect).Text, question.Options.Single(x => x.IsCorrect).Text);

        var previewPayload = JsonSerializer.Deserialize<QuestionPreviewPayloadDto>(previewCache.PreviewPayloadJson, JsonOptions);
        Assert.NotNull(previewPayload);
        Assert.Equal(draft.ContentHash, previewCache.ContentHash);
        Assert.Equal(QuestionValidationStatuses.Passed, previewPayload!.Summary.Status);
        Assert.Contains(previewPayload.SafePreviewFields.Keys, key => string.Equals(key, "text", StringComparison.Ordinal));
        Assert.Contains("\"Status\":\"passed\"", draft.LatestValidationResult.PreviewPayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateEditPublish_Flow_PersistsSecondVersion()
    {
        var createSaveResponse = await client.PostAsJsonAsync(
            "/api/questions/save-draft",
            new SaveQuestionDraftRequest(CreateValidRequest(questionId: null), "create"));
        createSaveResponse.EnsureSuccessStatusCode();
        var createSavePayload = await createSaveResponse.Content.ReadFromJsonAsync<SaveQuestionDraftResponse>();
        Assert.NotNull(createSavePayload);

        var createPublishResponse = await client.PostAsJsonAsync(
            "/api/questions/publish",
            new PublishQuestionRequest(createSavePayload.DraftId, "publish create"));
        createPublishResponse.EnsureSuccessStatusCode();
        var createPublishPayload = await createPublishResponse.Content.ReadFromJsonAsync<PublishQuestionResponse>();
        Assert.NotNull(createPublishPayload);
        Assert.True(createPublishPayload.Published);

        var editRequest = CreateValidRequest(questionId: createPublishPayload.QuestionId) with
        {
            Text = "Koliko je $2+2$?",
            CorrectAnswer = "$4$",
            Explanation = "Azurirano objasnjenje.",
            Options =
            [
                new QuestionAuthoringOptionDto(1, "$4$", true),
                new QuestionAuthoringOptionDto(2, "$3$", false),
                new QuestionAuthoringOptionDto(3, "$5$", false)
            ]
        };

        var editSaveResponse = await client.PostAsJsonAsync(
            "/api/questions/save-draft",
            new SaveQuestionDraftRequest(editRequest, "edit"));
        editSaveResponse.EnsureSuccessStatusCode();
        var editSavePayload = await editSaveResponse.Content.ReadFromJsonAsync<SaveQuestionDraftResponse>();
        Assert.NotNull(editSavePayload);

        var editPublishResponse = await client.PostAsJsonAsync(
            "/api/questions/publish",
            new PublishQuestionRequest(editSavePayload.DraftId, "publish edit"));
        editPublishResponse.EnsureSuccessStatusCode();
        var editPublishPayload = await editPublishResponse.Content.ReadFromJsonAsync<PublishQuestionResponse>();
        Assert.NotNull(editPublishPayload);
        Assert.True(editPublishPayload.Published);
        Assert.Equal(createPublishPayload.QuestionId, editPublishPayload.QuestionId);

        var versionsResponse = await client.GetAsync($"/api/questions/{editPublishPayload.QuestionId}/versions");
        versionsResponse.EnsureSuccessStatusCode();
        var versions = await versionsResponse.Content.ReadFromJsonAsync<List<QuestionVersionHistoryItemDto>>();

        Assert.NotNull(versions);
        Assert.True(versions.Count >= 2);
        Assert.Contains(versions, v => v.VersionNumber == 1);
        Assert.Contains(versions, v => v.VersionNumber == 2);
    }

    [Fact]
    public async Task SaveDraft_ThenPublish_OpenAnswer_ReturnsPublishedQuestion()
    {
        var request = new QuestionAuthoringRequest(
            null,
            "Izračunaj 2 + 3",
            "open_answer",
            "5",
            "Saberi brojeve.",
            2,
            1,
            1,
            [],
            [],
            [new StepExplanationAuthoringDto(1, "2 + 3 = 5", null, false)],
            "open-answer integration");

        var saveResponse = await client.PostAsJsonAsync(
            "/api/questions/save-draft",
            new SaveQuestionDraftRequest(request, "open answer"));

        saveResponse.EnsureSuccessStatusCode();
        var savePayload = await saveResponse.Content.ReadFromJsonAsync<SaveQuestionDraftResponse>();
        Assert.NotNull(savePayload);

        var publishResponse = await client.PostAsJsonAsync(
            "/api/questions/publish",
            new PublishQuestionRequest(savePayload.DraftId, "open answer publish"));

        publishResponse.EnsureSuccessStatusCode();
        var publishPayload = await publishResponse.Content.ReadFromJsonAsync<PublishQuestionResponse>();
        Assert.NotNull(publishPayload);
        Assert.True(publishPayload.Published);
        Assert.Equal("published", publishPayload.PublishState);
    }

    [Fact]
    public async Task Preview_SanitizesXssPayload_InSafePreviewFields()
    {
        var maliciousRequest = CreateValidRequest() with
        {
            Text = @"Koliko je $1+1$?<script>alert('xss')</script>",
            Explanation = @"<img src=x onerror=""alert(1)"">Objasnjenje",
            Options =
            [
                new QuestionAuthoringOptionDto(1, @"$2$<script>alert(2)</script>", true),
                new QuestionAuthoringOptionDto(2, "$3$", false),
                new QuestionAuthoringOptionDto(3, "$4$", false)
            ]
        };

        var response = await client.PostAsJsonAsync("/api/questions/preview", maliciousRequest);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PreviewQuestionResponse>();

        Assert.NotNull(payload);
        Assert.All(payload.Preview.SafePreviewFields.Values.Where(v => v is not null), value =>
            Assert.DoesNotContain("<script", value!, StringComparison.OrdinalIgnoreCase));
        Assert.All(payload.Preview.Normalized.Fields, field =>
            Assert.DoesNotContain("<script", field.NormalizedValue, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Revalidate_RebuildsDraftFromPublishedQuestion_AndUpdatesValidationHistory()
    {
        var saveResponse = await client.PostAsJsonAsync(
            "/api/questions/save-draft",
            new SaveQuestionDraftRequest(CreateValidRequest(questionId: null), "revalidate create"));
        saveResponse.EnsureSuccessStatusCode();
        var savePayload = await saveResponse.Content.ReadFromJsonAsync<SaveQuestionDraftResponse>();

        Assert.NotNull(savePayload);

        var publishResponse = await client.PostAsJsonAsync(
            "/api/questions/publish",
            new PublishQuestionRequest(savePayload.DraftId, "revalidate publish"));
        publishResponse.EnsureSuccessStatusCode();
        var publishPayload = await publishResponse.Content.ReadFromJsonAsync<PublishQuestionResponse>();

        Assert.NotNull(publishPayload);
        Assert.True(publishPayload.Published);

        await using (var db = CreateApiDbContext())
        {
            var question = await db.Questions
                .Include(x => x.Options)
                .SingleAsync(x => x.Id == publishPayload.QuestionId);

            question.SetCurrentDraft(null);
            question.SetText(@"Koliko je $\frac{1}{?$");
            await db.SaveChangesAsync();
        }

        var revalidateResponse = await client.PostAsync($"/api/questions/{publishPayload.QuestionId}/revalidate", null);
        revalidateResponse.EnsureSuccessStatusCode();
        var revalidatePayload = await revalidateResponse.Content.ReadFromJsonAsync<QuestionValidationHistoryDto>();

        Assert.NotNull(revalidatePayload);
        Assert.Equal(QuestionValidationStatuses.Failed, revalidatePayload.Status);
        Assert.True(revalidatePayload.HasErrors);
        Assert.Contains(revalidatePayload.Issues, issue =>
            issue.RuleId is "latex.unbalanced_braces" or "latex.invalid_fraction");

        var validationResponse = await client.GetAsync($"/api/questions/{publishPayload.QuestionId}/validation");
        validationResponse.EnsureSuccessStatusCode();
        var validationPayload = await validationResponse.Content.ReadFromJsonAsync<QuestionValidationHistoryDto>();

        Assert.NotNull(validationPayload);
        Assert.Equal(revalidatePayload.ValidationResultId, validationPayload!.ValidationResultId);

        await using var verifyDb = CreateApiDbContext();
        var updatedQuestion = await verifyDb.Questions.SingleAsync(x => x.Id == publishPayload.QuestionId);
        var rebuiltDraft = await verifyDb.QuestionDrafts
            .Include(x => x.LatestValidationResult)
            .SingleAsync(x => x.Id == updatedQuestion.CurrentDraftId);
        var previewCache = await verifyDb.QuestionPreviewCaches.SingleAsync(x => x.DraftId == rebuiltDraft.Id);

        Assert.Equal("system_revalidate", rebuiltDraft.ChangeReason);
        Assert.Equal(QuestionValidationStatuses.Failed, rebuiltDraft.ValidationStatus);
        Assert.Equal(revalidatePayload.ValidationResultId, rebuiltDraft.LatestValidationResultId);
        Assert.NotNull(rebuiltDraft.LatestValidationResult);
        Assert.Equal(rebuiltDraft.ContentHash, rebuiltDraft.LatestValidationResult!.ContentHash);
        Assert.Equal(rebuiltDraft.ContentHash, previewCache.ContentHash);
    }

    [Fact]
    public async Task Publish_InvalidDraft_ReturnsBadRequest()
    {
        var invalidRequest = CreateValidRequest() with
        {
            CorrectAnswer = "$5$",
            Options =
            [
                new QuestionAuthoringOptionDto(1, "$2$", true),
                new QuestionAuthoringOptionDto(2, "$3$", false),
                new QuestionAuthoringOptionDto(3, "$4$", false)
            ]
        };
        var saveResponse = await client.PostAsJsonAsync(
            "/api/questions/save-draft",
            new SaveQuestionDraftRequest(invalidRequest, "invalid"));

        saveResponse.EnsureSuccessStatusCode();
        var savePayload = await saveResponse.Content.ReadFromJsonAsync<SaveQuestionDraftResponse>();
        Assert.NotNull(savePayload);

        var publishResponse = await client.PostAsJsonAsync(
            "/api/questions/publish",
            new PublishQuestionRequest(savePayload.DraftId));

        Assert.Equal(HttpStatusCode.BadRequest, publishResponse.StatusCode);
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
            [
                new QuestionHintDto("formula", "$a+b$"),
                new QuestionHintDto("clue", "Pogledaj sabiranje."),
                new QuestionHintDto("full", "Rezultat je 2.")
            ],
            [new StepExplanationAuthoringDto(1, "$1+1=2$", null, false)],
            "integration");

    private ApiDbContext CreateApiDbContext()
    {
        var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApiDbContext>();
    }
}
