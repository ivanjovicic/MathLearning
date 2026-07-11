using MathLearning.Admin.Services;

namespace MathLearning.Tests.Admin;

public sealed class ReturnUrlSanitizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("dashboard")]
    [InlineData("https://evil.example/path")]
    [InlineData("//evil.example/path")]
    public void NormalizeLocalReturnUrl_InvalidOrExternalValue_ReturnsRoot(string? returnUrl)
    {
        var result = ReturnUrlSanitizer.NormalizeLocalReturnUrl(returnUrl);

        Assert.Equal("/", result);
    }

    [Fact]
    public void NormalizeLocalReturnUrl_MalformedRelativeUri_ReturnsRoot()
    {
        var malformed = "/broken" + '\0' + "path";

        var result = ReturnUrlSanitizer.NormalizeLocalReturnUrl(malformed);

        Assert.Equal("/", result);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/dashboard")]
    [InlineData("/questions/42?mode=edit#answer")]
    public void NormalizeLocalReturnUrl_ValidLocalPath_IsPreserved(string returnUrl)
    {
        var result = ReturnUrlSanitizer.NormalizeLocalReturnUrl(returnUrl);

        Assert.Equal(returnUrl, result);
    }
}
