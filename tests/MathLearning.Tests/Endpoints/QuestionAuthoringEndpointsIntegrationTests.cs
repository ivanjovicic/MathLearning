using System.Net;
using System.Net.Http.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Tests.Helpers;

namespace MathLearning.Tests.Endpoints;

public class QuestionAuthoringEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public QuestionAuthoringEndpointsIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
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
}
