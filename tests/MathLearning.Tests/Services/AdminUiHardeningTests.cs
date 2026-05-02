using MathLearning.Admin.Models;
using MathLearning.Admin.Services;

namespace MathLearning.Tests.Services;

public class AdminUiHardeningTests
{
    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("https://evil.com", "/")]
    [InlineData("//evil.com", "/")]
    [InlineData("admin", "/")]
    [InlineData("/admin/logs", "/admin/logs")]
    [InlineData("/questions/new?draft=1", "/questions/new?draft=1")]
    public void ReturnUrlSanitizer_NormalizesToLocalRoute(string? rawReturnUrl, string expected)
    {
        var normalized = ReturnUrlSanitizer.NormalizeLocalReturnUrl(rawReturnUrl);

        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void DeleteImpactFormatter_IncludesDependencyCounts()
    {
        var categoryMessage = DeleteImpactFormatter.BuildCategoryDeleteMessage("Algebra", 12);
        var questionMessage = DeleteImpactFormatter.BuildQuestionDeleteMessage(42, 4, 3);

        Assert.Contains("12", categoryMessage, StringComparison.Ordinal);
        Assert.Contains("blocked", categoryMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("42", questionMessage, StringComparison.Ordinal);
        Assert.Contains("4", questionMessage, StringComparison.Ordinal);
        Assert.Contains("3", questionMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void AppRazor_UsesAuthorizeRouteView()
    {
        var filePath = Path.Combine(FindRepositoryRoot(), "src", "MathLearning.Admin", "Components", "App.razor");
        var content = File.ReadAllText(filePath);

        Assert.Contains("<AuthorizeRouteView", content, StringComparison.Ordinal);
        Assert.Contains("<RedirectToLogin />", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_UsesConfiguredDataProtectionKeysPathBeforeDatabaseFallback()
    {
        var filePath = Path.Combine(FindRepositoryRoot(), "src", "MathLearning.Admin", "Program.cs");
        var content = File.ReadAllText(filePath);

        Assert.Contains("DataProtection:KeysPath", content, StringComparison.Ordinal);
        Assert.Contains("PersistKeysToFileSystem", content, StringComparison.Ordinal);
        Assert.Contains("PersistKeysToDbContext<AdminDbContext>", content, StringComparison.Ordinal);
    }

    [Fact]
    public void QuestionEditorValidation_RequiresSingleCorrectOptionForMultipleChoice()
    {
        var model = new QuestionEditorModel
        {
            Type = "multiple_choice",
            Text = "Sample question",
            CategoryId = 1,
            SubtopicId = 1,
            Options =
            [
                new() { Text = "A", IsCorrect = true },
                new() { Text = "B", IsCorrect = true }
            ]
        };

        var errors = QuestionEditorValidation.Validate(model);

        Assert.Contains(errors, message => message.Contains("samo jedan", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var markerPath = Path.Combine(directory.FullName, "src", "MathLearning.Admin", "MathLearning.Admin.csproj");
            if (File.Exists(markerPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
