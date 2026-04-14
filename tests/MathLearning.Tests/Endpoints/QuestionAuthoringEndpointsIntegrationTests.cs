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
