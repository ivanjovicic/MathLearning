using MathLearning.Application.Content;
using MathLearning.Domain.Enums;

namespace MathLearning.Tests.Content;

public sealed class MathContentSanitizerTests
{
    private readonly MathContentSanitizer sanitizer = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeMathContent_MissingInputReturnsEmptyString(string? input)
    {
        Assert.Equal(string.Empty, sanitizer.NormalizeMathContent(input, ContentFormat.MarkdownWithMath));
    }

    [Fact]
    public void NormalizeMathContent_TrimsNormalizesLineEndingsAndCollapsesSpacing()
    {
        const string input = "  prvi   red\r\n\r\n\r\n\r\n drugi\t\tdeo  ";

        var result = sanitizer.NormalizeMathContent(input, ContentFormat.MarkdownWithMath);

        Assert.Equal("prvi red\n\n drugi deo", result);
        Assert.DoesNotContain('\r', result);
    }

    [Fact]
    public void NormalizeMathContent_RemovesScriptBlocksAndQuotedEventAttributes()
    {
        const string input = "<div onclick=\"steal()\">Safe</div><script>alert('x')</script>";

        var result = sanitizer.NormalizeMathContent(input, ContentFormat.Html);

        Assert.Equal("<div>Safe</div>", result);
        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("steal", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeMathContent_StripsHtmlOutsideHtmlFormat()
    {
        const string input = "<strong>Važno</strong>: $x=2$";

        var result = sanitizer.NormalizeMathContent(input, ContentFormat.MarkdownWithMath);

        Assert.Equal("Važno: $x=2$", result);
    }

    [Fact]
    public void NormalizeMathContent_PreservesSafeHtmlInHtmlFormat()
    {
        const string input = "<strong>Važno</strong>: x=2";

        var result = sanitizer.NormalizeMathContent(input, ContentFormat.Html);

        Assert.Equal(input, result);
    }

    [Fact]
    public void NormalizeMathContent_NormalizesMathDelimitersAndUnicodeOperators()
    {
        const string input = @"\(a × b\) − \[c ÷ d\]";

        var result = sanitizer.NormalizeMathContent(input, ContentFormat.MarkdownWithMath);

        Assert.Equal(@"$a \times b$ - $$c \div d$$", result);
    }

    [Fact]
    public void GetWarnings_EmptyInputReturnsNoWarnings()
    {
        Assert.Empty(sanitizer.GetWarnings(null, ContentFormat.MarkdownWithMath));
        Assert.Empty(sanitizer.GetWarnings("   ", ContentFormat.MarkdownWithMath));
    }

    [Theory]
    [InlineData("{x", "Unbalanced braces detected.")]
    [InlineData("x}", "Unbalanced braces detected.")]
    [InlineData("$x+1", "Unbalanced inline math delimiters detected.")]
    [InlineData(@"\href{https://example.test}{x}", "Unsupported LaTeX commands detected.")]
    [InlineData(@"\includegraphics{secret.png}", "Unsupported LaTeX commands detected.")]
    [InlineData(@"\frac x y", @"Malformed \frac expression detected.")]
    [InlineData(@"\sqrt x", @"Malformed \sqrt expression detected.")]
    public void GetWarnings_DetectsMalformedOrUnsupportedMath(string input, string expectedWarning)
    {
        var warnings = sanitizer.GetWarnings(input, ContentFormat.MarkdownWithMath);

        Assert.Contains(expectedWarning, warnings);
    }

    [Fact]
    public void GetWarnings_EscapedDollarDoesNotCreateUnbalancedDelimiterWarning()
    {
        var warnings = sanitizer.GetWarnings(@"Cena je \$5", ContentFormat.PlainText);

        Assert.DoesNotContain("Unbalanced inline math delimiters detected.", warnings);
    }

    [Fact]
    public void GetWarnings_HtmlContentReportsSanitizationWarning()
    {
        var warnings = sanitizer.GetWarnings("<em>x</em>", ContentFormat.Html);

        Assert.Contains("HTML content will be sanitized before rendering.", warnings);
    }

    [Theory]
    [InlineData("$x+1$")]
    [InlineData(@"\frac{1}{2}")]
    [InlineData(@"\sqrt{x}")]
    public void GetWarnings_PlainTextMathReportsFormatMismatch(string input)
    {
        var warnings = sanitizer.GetWarnings(input, ContentFormat.PlainText);

        Assert.Contains(warnings, warning => warning.Contains("PlainText", StringComparison.Ordinal));
    }

    [Fact]
    public void GetWarnings_BalancedSupportedMathReturnsNoWarnings()
    {
        var warnings = sanitizer.GetWarnings(@"Reši $\frac{1}{2} + \sqrt{4}$.", ContentFormat.MarkdownWithMath);

        Assert.Empty(warnings);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateSemanticsAltText_MissingInputReturnsNull(string? input)
    {
        Assert.Null(sanitizer.GenerateSemanticsAltText(input, ContentFormat.MarkdownWithMath));
    }

    [Fact]
    public void GenerateSemanticsAltText_ConvertsCommonMathTokensToReadableText()
    {
        const string input = @"$\frac{a_1}{b} × \sqrt{x^2} ÷ y^3$";

        var result = sanitizer.GenerateSemanticsAltText(input, ContentFormat.MarkdownWithMath);

        Assert.Equal("fraction a sub 1 b times square root x squared divided by y cubed", result);
        Assert.DoesNotContain('$', result!);
        Assert.DoesNotContain('{', result!);
        Assert.DoesNotContain('}', result!);
    }

    [Fact]
    public void GenerateSemanticsAltText_RemovesUnsafeMarkupBeforeGeneratingDescription()
    {
        const string input = "<script>secret()</script><strong>x × 2</strong>";

        var result = sanitizer.GenerateSemanticsAltText(input, ContentFormat.MarkdownWithMath);

        Assert.Equal("x times 2", result);
        Assert.DoesNotContain("secret", result, StringComparison.OrdinalIgnoreCase);
    }
}
