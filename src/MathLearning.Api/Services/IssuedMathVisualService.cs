using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.QuestionAuthoring;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Services;

/// <summary>Captures only approved, exact-version POST aid in the server-side issued manifest.</summary>
public sealed class IssuedMathVisualService(ApiDbContext db, MathVisualContentPolicy policy, IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly bool enabled = configuration.GetValue<bool>("MathVisual:Enabled");
    private readonly byte[]? signingKey = ParseSigningKey(configuration["MathVisual:ManifestSigningKey"]);

    public async Task<IReadOnlyDictionary<int, IssuedMathVisualSnapshot>> BuildAsync(
        IReadOnlyList<Question> questions,
        string locale,
        Guid quizSessionId,
        string userId,
        CancellationToken cancellationToken)
    {
        if (!enabled || signingKey is null)
        {
            return new Dictionary<int, IssuedMathVisualSnapshot>();
        }

        var ids = questions.Where(x => x.CurrentVersionNumber > 0).Select(x => x.Id).ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, IssuedMathVisualSnapshot>();
        }

        var versions = await db.QuestionVersions.AsNoTracking()
            .Where(x => ids.Contains(x.QuestionId) && x.PublishState == QuestionPublishStates.Published)
            .ToListAsync(cancellationToken);
        var result = new Dictionary<int, IssuedMathVisualSnapshot>();

        foreach (var question in questions)
        {
            var contentLocale = ResolveLocale(question, locale);
            var matchingVersions = versions.Where(x =>
                x.QuestionId == question.Id && x.VersionNumber == question.CurrentVersionNumber).Take(2).ToArray();
            if (matchingVersions.Length != 1)
            {
                continue;
            }
            var version = matchingVersions[0];
            if (string.IsNullOrWhiteSpace(version.SnapshotJson))
            {
                continue;
            }

            QuestionAuthoringRequest? authored;
            try
            {
                authored = JsonSerializer.Deserialize<QuestionAuthoringRequest>(version.SnapshotJson, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            var plan = authored?.MathVisualPlan;
            if (authored is null || plan is null || authored.ContentPackId != MathVisualContentPolicy.PackId ||
                !string.Equals(plan.Locale, contentLocale, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var hash = MathVisualContentPolicy.ComputeContentHash(authored);
            if (policy.Validate(authored, hash).Count != 0 ||
                question.Text != authored.Text ||
                question.Type != authored.Type ||
                question.GetExpectedAnswerText() != authored.CorrectAnswer ||
                question.Explanation != authored.Explanation ||
                question.SemanticsAltText != authored.SemanticsAltText ||
                !string.IsNullOrWhiteSpace(question.HintFormula) ||
                !string.IsNullOrWhiteSpace(question.HintClue) ||
                !string.IsNullOrWhiteSpace(question.HintFull) ||
                question.Options.OrderBy(x => x.Order)
                    .Select(x => (x.Text, x.IsCorrect, x.SemanticsAltText))
                    .SequenceEqual(authored.Options.Select(x => (x.Text, x.IsCorrect, x.SemanticsAltText))) == false ||
                question.Steps.Count != authored.Steps.Count ||
                question.Steps.OrderBy(x => x.StepIndex)
                    .Select(x => (x.Text, x.Hint, x.Highlight))
                    .SequenceEqual(authored.Steps.OrderBy(x => x.Order).Select(x => (x.Text, x.Hint, x.Highlight))) == false)
            {
                continue;
            }

            var aid = new IssuedMathVisualSnapshot(
                plan.SchemaVersion,
                question.Id,
                version.VersionNumber,
                hash,
                plan.TemplateId,
                plan.TemplateVersion,
                plan.Locale,
                plan.Operation,
                plan.TaskShape,
                plan.Rows,
                plan.Columns,
                "multiplication_array",
                plan.Groups.ToArray(),
                authored.Steps.OrderBy(x => x.Order)
                    .Select(x => new IssuedMathVisualStepSnapshot(x.Order, x.Text)).ToArray(),
                "total",
                "server_confirmed");
            var issuedForSigning = IssuedQuestionContentSnapshot.FromQuestion(question, aid);
            result[question.Id] = aid with { IntegrityTag = Sign(issuedForSigning, quizSessionId, userId) };
        }

        return result;
    }

    public string ResolveLocale(Question question, string requestedLocale)
        => policy.PackSubtopicIds.Contains(question.SubtopicId) ? "sr" : requestedLocale;

    public bool IsDesignatedPackSubtopic(Question question)
        => policy.PackSubtopicIds.Contains(question.SubtopicId);

    public IssuedMathVisualSnapshot? ValidateForResponse(
        IssuedQuestionContentSnapshot? issued,
        string locale,
        Guid quizSessionId,
        string userId)
    {
        var aid = issued?.MathVisual;
        if (!enabled || signingKey is null || issued is null || aid is null ||
            issued.VersionNumber <= 0 || issued.Options is null || issued.Steps is null ||
            !VerifySignature(issued, quizSessionId, userId) ||
            aid.SchemaVersion != 1 || aid.QuestionId != issued.QuestionId ||
            aid.IssuedQuestionVersion != issued.VersionNumber || aid.TemplateVersion != 1 ||
            aid.Locale != locale || aid.Operation != "multiply" || aid.TaskShape != "product" ||
            aid.VisualAidType != "multiplication_array" || aid.RevealStage != "total" ||
            aid.AnswerSettlement != "server_confirmed" ||
            aid.Rows is < 2 or > 9 || aid.Columns is < 2 or > 9 ||
            aid.Groups is null || aid.ExplanationSteps is null ||
            !MathVisualContentPolicy.TryParseProductPrompt(issued.Text, out var rows, out var columns) ||
            rows != aid.Rows || columns != aid.Columns)
        {
            return null;
        }

        var product = aid.Rows * aid.Columns;
        var expected = product.ToString(CultureInfo.InvariantCulture);
        var correctOptions = issued.Options.Where(x => x.IsCorrect).ToArray();
        var correctOption = correctOptions.Length == 1 ? correctOptions[0].Text : null;
        if (issued.CorrectAnswer != expected || correctOption != expected ||
            aid.Groups.Length is < 1 or > 2 || aid.Groups.Any(x => x <= 0 || x > aid.Rows) ||
            aid.Groups.Sum(x => (long)x) != aid.Rows || aid.Groups.Sum(x => (long)x * aid.Columns) != product ||
            aid.ExplanationSteps.Length != issued.Steps.Length ||
            !aid.ExplanationSteps.OrderBy(x => x.Order).Select(x => x.Text)
                .SequenceEqual(issued.Steps.OrderBy(x => x.StepIndex).Select(x => x.Text)))
        {
            return null;
        }

        if (aid.TemplateId == "directArray")
        {
            return aid.Groups.Length == 1 && aid.Groups[0] == aid.Rows ? aid : null;
        }

        if (aid.TemplateId == "splitFive")
        {
            return aid.Rows > 5 && aid.Groups.Length == 2 &&
                   aid.Groups[0] == 5 && aid.Groups[1] == aid.Rows - 5 ? aid : null;
        }

        return null;
    }

    private static byte[]? ParseSigningKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            var bytes = Convert.FromBase64String(value);
            return bytes.Length >= 32 ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private string Sign(IssuedQuestionContentSnapshot issued, Guid quizSessionId, string userId)
    {
        var unsigned = issued with { MathVisual = issued.MathVisual! with { IntegrityTag = null } };
        var canonical = $"{quizSessionId:N}:{userId}:{JsonSerializer.Serialize(unsigned, JsonOptions)}";
        return Convert.ToHexString(HMACSHA256.HashData(signingKey!, Encoding.UTF8.GetBytes(canonical)));
    }

    private bool VerifySignature(IssuedQuestionContentSnapshot issued, Guid quizSessionId, string userId)
    {
        var aid = issued.MathVisual!;
        if (string.IsNullOrWhiteSpace(aid.IntegrityTag)) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(aid.IntegrityTag),
                Convert.FromHexString(Sign(issued, quizSessionId, userId)));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
