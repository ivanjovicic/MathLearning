using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Api.Middleware;
using MathLearning.Application.DTOs.Explanations;
using MathLearning.Application.Services;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MathLearning.Tests.Endpoints;

public sealed class ExplanationEndpointContractTests :
    IClassFixture<ExplanationContractWebApplicationFactory>,
    IAsyncLifetime
{
    private const string SecretNotFound = "SECRET_EXPLANATION_NOT_FOUND_DETAIL";
    private const string SecretFailure = "SECRET_EXPLANATION_UNEXPECTED_FAILURE";

    private readonly ExplanationContractWebApplicationFactory factory;
    private readonly HttpClient client;

    public ExplanationEndpointContractTests(ExplanationContractWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        factory.Service.Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AnonymousUser_IsDeniedBeforeAnyExplanationServiceCall()
    {
        using var get = AnonymousRequest(HttpMethod.Get, "/api/explanations/problem/7?lang=sr");
        using var generate = AnonymousRequest(
            HttpMethod.Post,
            "/api/explanations/generate",
            JsonContent.Create(ValidGenerateRequest()));
        using var mistake = AnonymousRequest(
            HttpMethod.Post,
            "/api/explanations/mistake-analysis",
            JsonContent.Create(ValidMistakeRequest()));

        var getResponse = await client.SendAsync(get);
        var generateResponse = await client.SendAsync(generate);
        var mistakeResponse = await client.SendAsync(mistake);

        AssertUnauthorizedOrForbidden(getResponse.StatusCode);
        AssertUnauthorizedOrForbidden(generateResponse.StatusCode);
        AssertUnauthorizedOrForbidden(mistakeResponse.StatusCode);
        Assert.Equal(0, factory.Service.TotalCalls);
    }

    [Fact]
    public async Task GetProblem_BlankLanguageDefaultsToEnglishAndForwardsCancellation()
    {
        factory.Service.ExplanationResponse = CreateExplanationResponse(problemId: 42, language: "en");
        using var request = AuthenticatedRequest(HttpMethod.Get, "/api/explanations/problem/42?lang=");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, factory.Service.GetCalls);
        Assert.Equal(42, factory.Service.LastProblemId);
        Assert.Equal("en", factory.Service.LastLanguage);
        Assert.True(factory.Service.LastCancellationToken.CanBeCanceled);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(42, json.RootElement.GetProperty("problemId").GetInt32());
        Assert.Equal("en", json.RootElement.GetProperty("language").GetString());
        Assert.True(json.RootElement.TryGetProperty("steps", out _));
    }

    [Fact]
    public async Task Generate_InvalidRequest_ReturnsValidationProblemWithoutServiceCall()
    {
        var invalid = new GenerateExplanationRequest(
            ProblemId: null,
            ProblemText: null,
            StudentAnswer: null,
            ExpectedAnswer: null,
            Topic: null,
            Subtopic: null,
            Grade: 5,
            Difficulty: "",
            Language: "");
        using var request = AuthenticatedRequest(
            HttpMethod.Post,
            "/api/explanations/generate",
            JsonContent.Create(invalid));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Service.GenerateCalls);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Either problemId or problemText is required.", body);
    }

    [Fact]
    public async Task MistakeAnalysis_InvalidStudentAnswer_ReturnsValidationProblemWithoutServiceCall()
    {
        var invalid = ValidMistakeRequest() with { StudentAnswer = "" };
        using var request = AuthenticatedRequest(
            HttpMethod.Post,
            "/api/explanations/mistake-analysis",
            JsonContent.Create(invalid));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Service.MistakeCalls);
    }

    [Theory]
    [InlineData("/api/explanations/generate", true)]
    [InlineData("/api/explanations/mistake-analysis", false)]
    public async Task ReferencedProblemNotFound_ReturnsStableMessageWithoutRawException(
        string path,
        bool generate)
    {
        factory.Service.ExceptionToThrow = new KeyNotFoundException(SecretNotFound);
        using var request = AuthenticatedRequest(
            HttpMethod.Post,
            path,
            generate
                ? JsonContent.Create(ValidGenerateRequest())
                : JsonContent.Create(ValidMistakeRequest()));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SecretNotFound, body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Referenced problem was not found.", json.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task StoredProblemNotFound_ReturnsStableMessageWithoutRawException()
    {
        factory.Service.ExceptionToThrow = new KeyNotFoundException(SecretNotFound);
        using var request = AuthenticatedRequest(HttpMethod.Get, "/api/explanations/problem/999");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SecretNotFound, body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Stored problem was not found.", json.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ValidMistakeAnalysis_DelegatesOnceAndReturnsStableResponseShape()
    {
        factory.Service.MistakeResponse = new MistakeAnalysisResponseDto(
            ProblemId: 17,
            ProblemText: "2 + 2",
            ProblemHash: "hash-17",
            ServedFromCache: false,
            Mistakes: Array.Empty<MistakeInsightDto>(),
            Steps: Array.Empty<StepExplanationItemDto>(),
            FormulaReferences: Array.Empty<FormulaReferenceDto>());
        using var request = AuthenticatedRequest(
            HttpMethod.Post,
            "/api/explanations/mistake-analysis",
            JsonContent.Create(ValidMistakeRequest()));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, factory.Service.MistakeCalls);
        Assert.True(factory.Service.LastCancellationToken.CanBeCanceled);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(17, json.RootElement.GetProperty("problemId").GetInt32());
        Assert.True(json.RootElement.TryGetProperty("mistakes", out _));
        Assert.True(json.RootElement.TryGetProperty("steps", out _));
    }

    [Fact]
    public async Task UnexpectedServiceFailure_ReturnsGenericErrorWithoutInternalMessage()
    {
        factory.Service.ExceptionToThrow = new InvalidOperationException(SecretFailure);
        using var request = AuthenticatedRequest(
            HttpMethod.Post,
            "/api/explanations/generate",
            JsonContent.Create(ValidGenerateRequest()));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SecretFailure, body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(
            SafeClientErrorResponse.GenericInternalError,
            json.RootElement.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
    }

    private static GenerateExplanationRequest ValidGenerateRequest() => new(
        ProblemId: 17,
        ProblemText: null,
        StudentAnswer: "4",
        ExpectedAnswer: "4",
        Topic: "Arithmetic",
        Subtopic: "Addition",
        Grade: 5,
        Difficulty: "easy",
        Language: "en");

    private static MistakeAnalysisRequest ValidMistakeRequest() => new(
        ProblemId: 17,
        ProblemText: null,
        StudentAnswer: "5",
        ExpectedAnswer: "4",
        Topic: "Arithmetic",
        Subtopic: "Addition",
        Grade: 5,
        Difficulty: "easy",
        Language: "en");

    private static ExplanationResponseDto CreateExplanationResponse(int? problemId, string language) => new(
        ProblemId: problemId,
        ProblemText: "2 + 2",
        ProblemHash: "hash",
        Language: language,
        ServedFromCache: false,
        Steps: Array.Empty<StepExplanationItemDto>(),
        FormulaReferences: Array.Empty<FormulaReferenceDto>(),
        Mistakes: Array.Empty<MistakeInsightDto>());

    private static HttpRequestMessage AnonymousRequest(
        HttpMethod method,
        string path,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");
        return request;
    }

    private static HttpRequestMessage AuthenticatedRequest(
        HttpMethod method,
        string path,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Add("X-Test-UserId", "explanation-user");
        return request;
    }

    private static void AssertUnauthorizedOrForbidden(HttpStatusCode statusCode) =>
        Assert.True(statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
}

public sealed class ExplanationContractWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    public RecordingStepExplanationService Service { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IStepExplanationService>();
            services.AddSingleton<IStepExplanationService>(Service);
        });
    }
}

public sealed class RecordingStepExplanationService : IStepExplanationService
{
    private int getCalls;
    private int generateCalls;
    private int mistakeCalls;

    public int GetCalls => Volatile.Read(ref getCalls);
    public int GenerateCalls => Volatile.Read(ref generateCalls);
    public int MistakeCalls => Volatile.Read(ref mistakeCalls);
    public int TotalCalls => GetCalls + GenerateCalls + MistakeCalls;
    public int LastProblemId { get; private set; }
    public string? LastLanguage { get; private set; }
    public CancellationToken LastCancellationToken { get; private set; }
    public Exception? ExceptionToThrow { get; set; }
    public ExplanationResponseDto ExplanationResponse { get; set; } = CreateDefaultExplanation();
    public MistakeAnalysisResponseDto MistakeResponse { get; set; } = CreateDefaultMistake();

    public void Reset()
    {
        Interlocked.Exchange(ref getCalls, 0);
        Interlocked.Exchange(ref generateCalls, 0);
        Interlocked.Exchange(ref mistakeCalls, 0);
        LastProblemId = 0;
        LastLanguage = null;
        LastCancellationToken = default;
        ExceptionToThrow = null;
        ExplanationResponse = CreateDefaultExplanation();
        MistakeResponse = CreateDefaultMistake();
    }

    public Task<ExplanationResponseDto> GetForProblemAsync(
        int problemId,
        string language,
        CancellationToken ct = default)
    {
        Interlocked.Increment(ref getCalls);
        LastProblemId = problemId;
        LastLanguage = language;
        LastCancellationToken = ct;
        ThrowIfConfigured();
        return Task.FromResult(ExplanationResponse);
    }

    public Task<ExplanationResponseDto> GenerateAsync(
        GenerateExplanationRequest request,
        CancellationToken ct = default)
    {
        Interlocked.Increment(ref generateCalls);
        LastProblemId = request.ProblemId ?? 0;
        LastLanguage = request.Language;
        LastCancellationToken = ct;
        ThrowIfConfigured();
        return Task.FromResult(ExplanationResponse);
    }

    public Task<MistakeAnalysisResponseDto> AnalyzeMistakeAsync(
        MistakeAnalysisRequest request,
        CancellationToken ct = default)
    {
        Interlocked.Increment(ref mistakeCalls);
        LastProblemId = request.ProblemId ?? 0;
        LastLanguage = request.Language;
        LastCancellationToken = ct;
        ThrowIfConfigured();
        return Task.FromResult(MistakeResponse);
    }

    private void ThrowIfConfigured()
    {
        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;
    }

    private static ExplanationResponseDto CreateDefaultExplanation() => new(
        17,
        "2 + 2",
        "hash",
        "en",
        false,
        Array.Empty<StepExplanationItemDto>(),
        Array.Empty<FormulaReferenceDto>(),
        Array.Empty<MistakeInsightDto>());

    private static MistakeAnalysisResponseDto CreateDefaultMistake() => new(
        17,
        "2 + 2",
        "hash",
        false,
        Array.Empty<MistakeInsightDto>(),
        Array.Empty<StepExplanationItemDto>(),
        Array.Empty<FormulaReferenceDto>());
}
